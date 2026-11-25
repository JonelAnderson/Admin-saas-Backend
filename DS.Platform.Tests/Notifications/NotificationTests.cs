using DS.Platform.Application.DTOs.Mail.Request;
using DS.Platform.Application.DTOs.Payments.Request;
using DS.Platform.Application.Features.Notifications.Commands.QueueCertificateNotices;
using DS.Platform.Application.Features.Notifications.Commands.Retry;
using DS.Platform.Application.Features.Notifications.Commands.Send;
using DS.Platform.Application.Features.Notifications.Common;
using DS.Platform.Application.Features.Payments.Commands.Register;
using DS.Platform.Application.Features.Subscriptions.Commands.Evaluate;
using DS.Platform.Application.Interfaces.Repositories;
using DS.Platform.Application.Interfaces.Services;
using DS.Platform.Application.Interfaces.Services.ApiSunat;
using DS.Platform.Domain.Entities.ApiSunat;
using DS.Platform.Domain.Entities.Billing;
using DS.Platform.Domain.Entities.Customers;
using DS.Platform.Domain.Entities.Ops;
using DS.Platform.Domain.Enums;
using DS.Platform.Shared.Wrapper;
using DS.Platform.Tests.Customers;
using DS.Platform.Tests.Support;
using Microsoft.EntityFrameworkCore;

namespace DS.Platform.Tests.Notifications
{
    /// <summary>Correo de mentira: guarda lo enviado y puede fallar a pedido o estar sin configurar.</summary>
    public sealed class FakeMail : IMailService
    {
        public bool IsConfigured { get; set; } = true;
        public Exception? FailWith { get; set; }
        public List<MailRequest> Sent { get; } = new();

        public Task SendAsync(MailRequest request)
        {
            if (FailWith is not null)
            {
                throw FailWith;
            }
            Sent.Add(request);
            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// Avisos por correo (fase C2): las reglas puras, que cada aviso se encole una sola vez por periodo, la constancia de pago en el
    /// mismo guardado, el envío con reintentos y caducidad, los avisos de certificados y la lectura de la bandeja.
    /// </summary>
    public class NotificationTests
    {
        private const int Alfa = 1, Beta = 2, ErpPlan = 10;
        private const long TenantAlfa = 501, TenantFree = 999;
        private static readonly DateOnly Today = new(2026, 11, 1);

        private readonly TestDatabase _database = new();
        private readonly TestCurrentUserService _user = TestCurrentUserService.User("cobranzas");
        private readonly TestClock _clock = new(Today);
        private readonly FakeMail _mail = new();
        private readonly FakeApiSunatAdminClient _api = new();

        public NotificationTests()
        {
            _database.SeedAsync(db =>
            {
                db.Set<Customer>().AddRange(
                    new Customer { Id = Alfa, Ruc = CustomerHandlersTests.RucAlfa, BusinessName = "ALFA <SAC>", ContactEmail = "pagos@alfa.pe" },
                    new Customer { Id = Beta, Ruc = CustomerHandlersTests.RucBeta, BusinessName = "BETA SAC" });
                db.Set<Plan>().Add(new Plan { Id = ErpPlan, Product = PlanProduct.Erp, Code = "ERP", Name = "ERP Básico", MonthlyPrice = 350m });
                db.Set<ApiSunatIssuer>().Add(new ApiSunatIssuer
                {
                    Id = 1, CustomerId = Alfa, TenantId = TenantAlfa, Ruc = "20100000001", BusinessName = "ALFA EMISORA", IsActive = true
                });
            }).GetAwaiter().GetResult();
        }

        private async Task<T> SendAsync<T>(Func<IUnitOfWork<int>, Task<T>> send)
        {
            await using var db = _database.CreateContext(_user);
            return await send(HandlerKit.UnitOfWork(db, _user));
        }

        private Task SubscribeAsync(int customer, DateOnly paidUntil, bool suspendedManually = false)
            => _database.SeedAsync(db => db.Set<Subscription>().Add(new Subscription
            {
                CustomerId = customer, PlanId = ErpPlan, Product = PlanProduct.Erp, StartOn = Today.AddDays(-60), BillingDay = 1, GraceDays = 5,
                Price = 350m, PaidUntil = paidUntil, CoveredUntil = paidUntil, Status = SubscriptionStatus.Active, IsActive = true,
                SuspendedManually = suspendedManually
            }));

        private Task<Result<int>> EvaluateAsync() => SendAsync(uow =>
            new EvaluateSubscriptionsCommandHandler(uow, _clock).Handle(new EvaluateSubscriptionsCommand(), CancellationToken.None));

        private Task<Result<int>> DispatchAsync() => SendAsync(uow =>
            new SendNotificationsCommandHandler(uow, _mail, _clock).Handle(new SendNotificationsCommand(), CancellationToken.None));

        private Task<Result<int>> QueueCertificatesAsync() => SendAsync(uow =>
            new QueueCertificateNoticesCommandHandler(uow, _api, _clock).Handle(new QueueCertificateNoticesCommand(), CancellationToken.None));

        private async Task<List<Notification>> NotificationsAsync()
        {
            await using var db = _database.CreateContext(_user);
            return await db.Set<Notification>().AsNoTracking().OrderBy(n => n.Id).ToListAsync();
        }

        private static Subscription Sub(SubscriptionStatus status, DateOnly covered, bool manual = false)
            => new() { Id = 7, CustomerId = 1, PlanId = 1, Product = PlanProduct.Erp, Status = status, CoveredUntil = covered, SuspendedManually = manual };

        // ------------------------------------------------------------------ reglas puras

        [Theory]
        [InlineData(6, null)]
        [InlineData(5, NotificationType.DueSoon)]
        [InlineData(1, NotificationType.DueSoon)]
        [InlineData(0, NotificationType.DueToday)]
        public void The_rule_warns_five_days_ahead_and_on_the_due_day(int daysLeft, NotificationType? expected)
        {
            var due = NoticeRules.For(Sub(SubscriptionStatus.Active, Today.AddDays(daysLeft)), Today);
            Assert.Equal(expected, due?.Type);
            // La prueba que termina se avisa igual que lo pagado.
            Assert.Equal(expected, NoticeRules.For(Sub(SubscriptionStatus.Trial, Today.AddDays(daysLeft)), Today)?.Type);
        }

        [Fact]
        public void The_rule_covers_grace_and_automatic_suspension_but_not_manual_or_cancelled()
        {
            Assert.Equal(NotificationType.GraceEntered, NoticeRules.For(Sub(SubscriptionStatus.Grace, Today.AddDays(-2)), Today)!.Type);
            Assert.Equal(NotificationType.Suspended, NoticeRules.For(Sub(SubscriptionStatus.Suspended, Today.AddDays(-9)), Today)!.Type);
            Assert.Null(NoticeRules.For(Sub(SubscriptionStatus.Suspended, Today.AddDays(-9), manual: true), Today));
            Assert.Null(NoticeRules.For(Sub(SubscriptionStatus.Cancelled, Today.AddDays(-9)), Today));
        }

        [Fact]
        public void The_key_changes_with_the_covered_period_so_the_next_cycle_warns_again()
        {
            var a = NoticeRules.For(Sub(SubscriptionStatus.Active, Today.AddDays(3)), Today)!.DedupKey;
            var sameCycleTomorrow = NoticeRules.For(Sub(SubscriptionStatus.Active, Today.AddDays(3)), Today.AddDays(1))!.DedupKey;
            var nextCycle = NoticeRules.For(Sub(SubscriptionStatus.Active, Today.AddDays(33)), Today.AddDays(31))!.DedupKey;
            Assert.Equal(a, sameCycleTomorrow);
            Assert.NotEqual(a, nextCycle);
        }

        [Theory]
        [InlineData(45, null)]
        [InlineData(30, 30)]
        [InlineData(8, 30)]
        [InlineData(7, 7)]
        [InlineData(2, 7)]
        [InlineData(1, 1)]
        [InlineData(0, 1)]
        [InlineData(-3, 0)]
        public void The_certificate_buckets_are_30_7_1_and_expired(long daysLeft, int? bucket)
            => Assert.Equal(bucket, NoticeRules.CertificateBucket(daysLeft));

        [Fact]
        public void The_templates_encode_user_text_and_format_money_and_dates_for_peru()
        {
            var (subject, body) = NoticeTemplates.Build(NotificationType.DueSoon, new NoticeContext(
                "ALFA <SAC> & Cía", "ERP", "Básico", new DateOnly(2026, 11, 30), null, "PEN", 1234.5m));
            Assert.Contains("30/11/2026", subject);
            Assert.Contains("ALFA &lt;SAC&gt; &amp; Cía", body);
            Assert.DoesNotContain("<SAC>", body);
            Assert.Contains("S/ 1,234.50", body);
            Assert.Contains("US$ 10.00", NoticeTemplates.Build(NotificationType.DueToday,
                new NoticeContext("X", "ERP", "B", Today, null, "USD", 10m)).Body);
        }

        // ------------------------------------------------------------------ avisos de cobro

        [Fact]
        public async Task The_daily_evaluation_queues_each_notice_once_per_period()
        {
            await SubscribeAsync(Alfa, Today.AddDays(3));

            await EvaluateAsync();
            var queued = Assert.Single(await NotificationsAsync());
            Assert.Equal((NotificationType.DueSoon, NotificationStatus.Pending, "pagos@alfa.pe"), (queued.Type, queued.Status, queued.ToEmail));
            Assert.Contains("ALFA &lt;SAC&gt;", queued.Body);
            Assert.Contains("04/11/2026", queued.Subject);

            // El job puede correr varias veces, y al día siguiente: el mismo periodo no repite el aviso.
            await EvaluateAsync();
            _clock.SetLimaDate(Today.AddDays(1));
            await EvaluateAsync();
            Assert.Single(await NotificationsAsync());
        }

        [Fact]
        public async Task A_customer_without_email_leaves_the_notice_skipped_and_visible()
        {
            await SubscribeAsync(Beta, Today.AddDays(2));

            await EvaluateAsync();
            await EvaluateAsync();

            var notice = Assert.Single(await NotificationsAsync());
            Assert.Equal((NotificationStatus.Skipped, null, NotificationOutbox.NoEmailMessage), (notice.Status, notice.ToEmail, notice.LastError));
            Assert.Equal(0, (await DispatchAsync()).Data);
            Assert.Empty(_mail.Sent);
        }

        [Fact]
        public async Task Grace_and_automatic_suspension_queue_their_notices_but_a_manual_suspension_does_not()
        {
            await SubscribeAsync(Alfa, Today.AddDays(-2));                       // en gracia
            await SubscribeAsync(Beta, Today.AddDays(-30), suspendedManually: true);

            await EvaluateAsync();

            var notices = await NotificationsAsync();
            Assert.Equal([NotificationType.GraceEntered], notices.Select(n => n.Type).ToArray());
        }

        // ------------------------------------------------------------------ constancia de pago

        [Fact]
        public async Task Registering_a_payment_queues_the_receipt_in_the_same_save()
        {
            await SubscribeAsync(Alfa, Today.AddDays(-1));
            await using var read = _database.CreateContext(_user);
            var subscriptionId = await read.Set<Subscription>().AsNoTracking().Select(s => s.Id).SingleAsync();

            var paid = await SendAsync(uow => new RegisterPaymentCommandHandler(uow, _clock, new TestLocalizer()).Handle(new RegisterPaymentCommand
            {
                RegisterPaymentRequestDto = new RegisterPaymentRequestDto
                {
                    SubscriptionId = subscriptionId, Amount = 350m, Method = PaymentMethod.Yape, OperationNumber = "op-77"
                }
            }, CancellationToken.None));
            Assert.True(paid.Succeeded, string.Join(" ", paid.Messages));

            var receipt = Assert.Single(await NotificationsAsync(), n => n.Type == NotificationType.PaymentRegistered);
            Assert.Equal((NotificationStatus.Pending, paid.Data, "pagos@alfa.pe"), (receipt.Status, receipt.PaymentId ?? 0, receipt.ToEmail));
            Assert.Contains("Constancia de pago", receipt.Subject);
            Assert.Contains("OP-77", receipt.Body);
            Assert.Contains("Yape", receipt.Body);
            Assert.Contains("S/ 350.00", receipt.Body);
        }

        // ------------------------------------------------------------------ envío

        [Fact]
        public async Task The_dispatcher_sends_pending_notices_once()
        {
            await SubscribeAsync(Alfa, Today.AddDays(3));
            await EvaluateAsync();

            Assert.Equal(1, (await DispatchAsync()).Data);
            Assert.Equal(0, (await DispatchAsync()).Data);

            var mail = Assert.Single(_mail.Sent);
            Assert.Equal("pagos@alfa.pe", mail.To);
            var notice = Assert.Single(await NotificationsAsync());
            Assert.Equal((NotificationStatus.Sent, 1), (notice.Status, notice.Attempts));
            Assert.NotNull(notice.SentOnUtc);
        }

        [Fact]
        public async Task Without_a_mail_server_notices_wait_untouched()
        {
            await SubscribeAsync(Alfa, Today.AddDays(3));
            await EvaluateAsync();
            _mail.IsConfigured = false;

            var result = await DispatchAsync();

            Assert.Equal(0, result.Data);
            var notice = Assert.Single(await NotificationsAsync());
            Assert.Equal((NotificationStatus.Pending, 0), (notice.Status, notice.Attempts));
        }

        [Fact]
        public async Task A_failing_server_retries_with_backoff_and_then_abandons_and_a_manual_retry_resets_it()
        {
            await SubscribeAsync(Alfa, Today.AddDays(3));
            await EvaluateAsync();
            _mail.FailWith = new InvalidOperationException("SMTP caído");

            await DispatchAsync();
            var first = Assert.Single(await NotificationsAsync());
            Assert.Equal((NotificationStatus.Failed, 1, "SMTP caído"), (first.Status, first.Attempts, first.LastError));
            Assert.True(first.NextAttemptOnUtc > _clock.NowUtc);

            // Antes de la hora de reintento no se toca; al llegar cada hora se reintenta hasta agotar los 5 intentos (en menos de un día).
            await DispatchAsync();
            Assert.Equal(1, Assert.Single(await NotificationsAsync()).Attempts);
            for (var attempt = 2; attempt <= NotificationRetryPolicy.MaxAttempts; attempt++)
            {
                _clock.Advance(NotificationRetryPolicy.DelayAfter(attempt - 1) + TimeSpan.FromMinutes(1));
                await DispatchAsync();
            }
            var abandoned = Assert.Single(await NotificationsAsync());
            Assert.Equal((NotificationStatus.Abandoned, NotificationRetryPolicy.MaxAttempts), (abandoned.Status, abandoned.Attempts));

            _mail.FailWith = null;
            var retry = await SendAsync(uow => new RetryNotificationCommandHandler(uow, _clock, new TestLocalizer())
                .Handle(new RetryNotificationCommand { Id = abandoned.Id }, CancellationToken.None));
            Assert.True(retry.Succeeded);
            Assert.Equal(1, (await DispatchAsync()).Data);
            Assert.Equal(NotificationStatus.Sent, Assert.Single(await NotificationsAsync()).Status);
        }

        [Fact]
        public async Task Only_failed_or_abandoned_notices_can_be_retried()
        {
            await SubscribeAsync(Alfa, Today.AddDays(3));
            await EvaluateAsync();
            var id = Assert.Single(await NotificationsAsync()).Id;

            var result = await SendAsync(uow => new RetryNotificationCommandHandler(uow, _clock, new TestLocalizer())
                .Handle(new RetryNotificationCommand { Id = id }, CancellationToken.None));

            Assert.False(result.Succeeded);
            Assert.Contains(RetryNotificationCommandHandler.NotRetryableMessage, result.Messages);
        }

        [Fact]
        public void The_retry_policy_backs_off_and_expires_stale_notices()
        {
            Assert.True(NotificationRetryPolicy.DelayAfter(2) > NotificationRetryPolicy.DelayAfter(1));
            Assert.Equal(NotificationRetryPolicy.DelayAfter(4), NotificationRetryPolicy.DelayAfter(40));
            Assert.False(NotificationRetryPolicy.IsExhausted(4));
            Assert.True(NotificationRetryPolicy.IsExhausted(5));
            Assert.False(NotificationRetryPolicy.IsExpired(_clock.NowUtc.AddDays(-2), _clock.NowUtc));
            Assert.True(NotificationRetryPolicy.IsExpired(_clock.NowUtc.AddDays(-4), _clock.NowUtc));
        }

        // ------------------------------------------------------------------ certificados

        private static ApiCertificateInfo Cert(long tenant, string status, long daysLeft)
            => new(tenant, "20100000001", "ALFA EMISORA", "production", status, Today.AddDays((int)daysLeft), daysLeft);

        [Fact]
        public async Task Certificate_notices_go_to_the_owner_once_per_bucket_and_restart_after_renewal()
        {
            _api.Certificates.Add(Cert(TenantAlfa, "POR_VENCER", 20));
            Assert.Equal(1, (await QueueCertificatesAsync()).Data);
            Assert.Equal(0, (await QueueCertificatesAsync()).Data);   // mismo tramo: no repite

            _api.Certificates.Clear();
            _api.Certificates.Add(Cert(TenantAlfa, "POR_VENCER", 6));  // entró al tramo de 7 días
            Assert.Equal(1, (await QueueCertificatesAsync()).Data);

            _api.Certificates.Clear();
            _api.Certificates.Add(Cert(TenantAlfa, "VENCIDO", -2));
            Assert.Equal(1, (await QueueCertificatesAsync()).Data);

            _api.Certificates.Clear();
            _api.Certificates.Add(Cert(TenantAlfa, "POR_VENCER", 25));  // renovado con otra fecha: vuelve a avisar
            Assert.Equal(1, (await QueueCertificatesAsync()).Data);

            var notices = await NotificationsAsync();
            Assert.All(notices, n => Assert.Equal((NotificationType.CertificateExpiring, Alfa, (long?)TenantAlfa), (n.Type, n.CustomerId, n.TenantId)));
            Assert.Equal(4, notices.Count);
            Assert.Contains(notices, n => n.Subject.Contains("venció"));
        }

        [Fact]
        public async Task Certificates_of_unlinked_companies_or_far_from_expiring_are_ignored_and_a_down_api_fails_cleanly()
        {
            _api.Certificates.Add(Cert(TenantFree, "POR_VENCER", 3));     // empresa que no es de ningún cliente
            _api.Certificates.Add(Cert(TenantAlfa, "POR_VENCER", 90));    // lejos
            Assert.Equal(0, (await QueueCertificatesAsync()).Data);

            _api.Down = true;
            var down = await QueueCertificatesAsync();
            Assert.False(down.Succeeded);
            Assert.Empty(await NotificationsAsync());
        }
    }
}
