using DS.Platform.Application.DTOs.Portal.Request;
using DS.Platform.Application.Features.Notifications.Commands.Retry;
using DS.Platform.Application.Features.Notifications.Commands.Send;
using DS.Platform.Application.Features.Notifications.Common;
using DS.Platform.Application.Features.Portal.Commands.Dispatch;
using DS.Platform.Application.Features.Portal.Commands.Invite;
using DS.Platform.Application.Features.Portal.Commands.Retry;
using DS.Platform.Application.Features.Portal.Commands.SetEnabled;
using DS.Platform.Application.Features.Portal.Common;
using DS.Platform.Application.Features.Portal.Queries.GetAllPaged;
using DS.Platform.Application.Features.Portal.Validators;
using DS.Platform.Application.Interfaces.Repositories;
using DS.Platform.Application.Interfaces.Services.ApiSunat;
using DS.Platform.Application.Interfaces.Services.Portal;
using DS.Platform.Domain.Entities.ApiSunat;
using DS.Platform.Domain.Entities.Customers;
using DS.Platform.Domain.Entities.Ops;
using DS.Platform.Domain.Enums;
using DS.Platform.Infrastructure.Contexts;
using DS.Platform.Shared.Wrapper;
using DS.Platform.Tests.Customers;
using DS.Platform.Tests.Notifications;
using DS.Platform.Tests.Support;
using Microsoft.EntityFrameworkCore;

namespace DS.Platform.Tests.Portal
{
    internal sealed class FakePortalSettings : IPortalSettings
    {
        public string? PublicUrl { get; set; } = "https://portal.midominio.pe/";
        public bool IsConfigured => PublicUrl is not null && PublicUrl.StartsWith("https://");
    }

    /// <summary>
    /// Acceso al portal de clientes de la API SUNAT (fase C6): la invitación solo guarda la bandeja, el despachador llama a la API y
    /// encola el aviso con el enlace de activación (que no queda en ningún otro lado), habilitar / deshabilitar, reintentos y la
    /// lectura del listado.
    /// </summary>
    public class PortalAccessTests
    {
        private const int Alfa = 1, Beta = 2;
        private const int IssuerA1 = 1, IssuerA2 = 2, IssuerB1 = 3;
        private const string Token = "tok-secreto-1";

        private readonly TestDatabase _database = new();
        private readonly TestCurrentUserService _user = TestCurrentUserService.User("soporte");
        private readonly TestClock _clock = new(new DateOnly(2026, 11, 24));
        private readonly FakeApiSunatAdminClient _api = new();
        private readonly FakePortalSettings _settings = new();
        private readonly FakeMail _mail = new();

        public PortalAccessTests()
        {
            _database.SeedAsync(db =>
            {
                db.Set<Customer>().AddRange(
                    new Customer { Id = Alfa, Ruc = CustomerHandlersTests.RucAlfa, BusinessName = "ALFA <SAC>", ContactEmail = "pagos@alfa.pe" },
                    new Customer { Id = Beta, Ruc = CustomerHandlersTests.RucBeta, BusinessName = "BETA SAC" });
                db.Set<ApiSunatIssuer>().AddRange(
                    new ApiSunatIssuer { Id = IssuerA1, CustomerId = Alfa, TenantId = 501, Ruc = "20100000001", BusinessName = "ALFA UNO" },
                    new ApiSunatIssuer { Id = IssuerA2, CustomerId = Alfa, TenantId = 502, Ruc = "20100000002", BusinessName = "ALFA DOS" },
                    new ApiSunatIssuer { Id = IssuerB1, CustomerId = Beta, TenantId = 601, Ruc = "20100000003", BusinessName = "BETA UNO" });
            }).GetAwaiter().GetResult();
        }

        private async Task<T> SendAsync<T>(Func<IUnitOfWork<int>, Task<T>> send)
        {
            await using var db = _database.CreateContext(_user);
            return await send(HandlerKit.UnitOfWork(db, _user));
        }

        private static InviteToPortalRequestDto Form(string email = "Ana@Alfa.pe ", int customer = Alfa, int[]? issuers = null, bool resetMfa = false)
            => new() { CustomerId = customer, Email = email, Name = "Ana Pérez", IssuerIds = (issuers ?? [IssuerA1]).ToList(), ResetMfa = resetMfa };

        private Task<Result<int>> InviteAsync(InviteToPortalRequestDto dto) => SendAsync(uow =>
            new InviteToPortalCommandHandler(uow, _clock).Handle(new InviteToPortalCommand { InviteToPortalRequestDto = dto }, CancellationToken.None));

        private Task<Result<int>> DispatchAsync() => SendAsync(uow =>
            new DispatchPortalAccessesCommandHandler(uow, _api, _settings, _clock).Handle(new DispatchPortalAccessesCommand(), CancellationToken.None));

        private Task<Result<int>> SetEnabledAsync(int id, bool enabled) => SendAsync(uow =>
            new SetPortalAccessEnabledCommandHandler(uow, _clock).Handle(new SetPortalAccessEnabledCommand { Id = id, Enabled = enabled }, CancellationToken.None));

        private Task<Result<int>> RetryAsync(int id) => SendAsync(uow =>
            new RetryPortalAccessCommandHandler(uow, _clock).Handle(new RetryPortalAccessCommand { Id = id }, CancellationToken.None));

        private Task<Result<int>> SendMailAsync() => SendAsync(uow =>
            new SendNotificationsCommandHandler(uow, _mail, _clock).Handle(new SendNotificationsCommand(), CancellationToken.None));

        private async Task<PortalAccess> AccessAsync(int id)
        {
            await using var db = _database.CreateContext(_user);
            return await db.Set<PortalAccess>().AsNoTracking().Include(a => a.Issuers).SingleAsync(a => a.Id == id);
        }

        private async Task<List<Notification>> NotificationsAsync()
        {
            await using var db = _database.CreateContext(_user);
            return await db.Set<Notification>().AsNoTracking().OrderBy(n => n.Id).ToListAsync();
        }

        /// <summary>Invita y despacha: deja a la persona con usuario en la API.</summary>
        private async Task<int> InvitedAsync(InviteToPortalRequestDto? dto = null)
        {
            var id = (await InviteAsync(dto ?? Form())).Data;
            await DispatchAsync();
            return id;
        }

        // ------------------------------------------------------------------ reglas puras

        [Fact]
        public void The_email_is_the_user_so_it_is_trimmed_and_lowercased()
            => Assert.Equal("ana@alfa.pe", PortalRules.NormalizeEmail("  Ana@Alfa.PE "));

        [Fact]
        public void The_activation_link_goes_to_the_portal_and_encodes_the_token()
        {
            Assert.Equal("https://portal.midominio.pe/activar?token=a%2Bb%2Fc%3D",
                PortalRules.ActivationUrl("https://portal.midominio.pe/", "a+b/c="));
            Assert.Equal("https://portal.midominio.pe/activar?token=x", PortalRules.ActivationUrl("https://portal.midominio.pe", "x"));
        }

        [Fact]
        public void The_invitation_email_encodes_user_text()
        {
            var (subject, body) = NoticeTemplates.PortalInvitationEmail(new PortalInvitationNotice(
                "Ana <script>", "ALFA <SAC>", "https://portal.midominio.pe/activar?token=x&y=1", 72, ["ALFA UNO (RUC 20100000001)"]));
            Assert.Contains("portal", subject);
            Assert.DoesNotContain("<script>", body);
            Assert.DoesNotContain("<SAC>", body);
            Assert.Contains("ALFA &lt;SAC&gt;", body);
            Assert.Contains("72 horas", body);
            Assert.Contains("ALFA UNO (RUC 20100000001)", body);
        }

        [Fact]
        public void The_validator_requires_a_valid_email_and_at_least_one_issuer()
        {
            var validator = new InviteToPortalCommandValidator();
            Assert.True(validator.Validate(new InviteToPortalCommand { InviteToPortalRequestDto = Form("ana@alfa.pe") }).IsValid);
            Assert.False(validator.Validate(new InviteToPortalCommand { InviteToPortalRequestDto = Form("no-es-correo") }).IsValid);
            Assert.False(validator.Validate(new InviteToPortalCommand { InviteToPortalRequestDto = Form(issuers: []) }).IsValid);
            Assert.False(validator.Validate(new InviteToPortalCommand { InviteToPortalRequestDto = Form(customer: 0) }).IsValid);
        }

        [Fact]
        public void The_invitation_token_never_shows_in_the_text_of_the_api_result()
        {
            var call = ApiSunatCallResult<ApiPortalInvitation>.Ok(new ApiPortalInvitation(7, Token, DateTime.UtcNow));
            Assert.DoesNotContain(Token, call.ToString());
        }

        // ------------------------------------------------------------------ invitar

        [Fact]
        public async Task Inviting_only_saves_a_pending_access_and_never_calls_the_api()
        {
            var result = await InviteAsync(Form(resetMfa: true));

            Assert.True(result.Succeeded);
            var access = await AccessAsync(result.Data);
            Assert.Equal("ana@alfa.pe", access.Email);
            Assert.Equal((PortalWorkType.Invite, RemoteActionStatus.Pending, true, true), (access.WorkType, access.Status, access.IsEnabled, access.ResetMfa));
            Assert.Equal([IssuerA1], access.Issuers.Select(i => i.IssuerId));
            Assert.Null(access.ApiUserId);
            Assert.Empty(_api.InvitationCalls);
            Assert.Empty(await NotificationsAsync());
        }

        [Fact]
        public async Task Inviting_rejects_unknown_inactive_customers_and_issuers_of_someone_else()
        {
            Assert.Equal(InviteToPortalCommandHandler.CustomerNotFoundMessage, (await InviteAsync(Form(customer: 99))).Messages.Single());
            Assert.Equal(InviteToPortalCommandHandler.IssuerNotOfCustomerMessage, (await InviteAsync(Form(issuers: [IssuerB1]))).Messages.Single());
            Assert.Equal(InviteToPortalCommandHandler.IssuerNotOfCustomerMessage, (await InviteAsync(Form(issuers: [IssuerA1, 99]))).Messages.Single());

            await _database.SeedAsync(db => db.Set<Customer>().Single(c => c.Id == Beta).IsActive = false);
            Assert.Equal(InviteToPortalCommandHandler.CustomerInactiveMessage, (await InviteAsync(Form(customer: Beta, issuers: [IssuerB1]))).Messages.Single());
        }

        [Fact]
        public async Task An_email_belongs_to_a_single_customer()
        {
            await InviteAsync(Form("ana@alfa.pe"));

            var other = await InviteAsync(Form("ANA@alfa.pe", Beta, [IssuerB1]));

            Assert.False(other.Succeeded);
            Assert.Equal(InviteToPortalCommandHandler.EmailOfOtherCustomerMessage, other.Messages.Single());
        }

        [Fact]
        public async Task Inviting_again_reuses_the_row_replaces_the_issuers_and_enables_the_access()
        {
            var id = await InvitedAsync();
            await SetEnabledAsync(id, false);
            await DispatchAsync();

            var again = await InviteAsync(Form("ANA@ALFA.PE", issuers: [IssuerA2], resetMfa: true));

            Assert.Equal(id, again.Data);
            var access = await AccessAsync(id);
            Assert.Equal([IssuerA2], access.Issuers.Select(i => i.IssuerId));
            Assert.Equal((PortalWorkType.Invite, RemoteActionStatus.Pending, true, true), (access.WorkType, access.Status, access.IsEnabled, access.ResetMfa));
            Assert.Equal(1, access.InvitationCount);
            await using var db = _database.CreateContext(_user);
            Assert.Equal(1, await db.Set<PortalAccess>().CountAsync());
        }

        // ------------------------------------------------------------------ despachar

        [Fact]
        public async Task The_dispatcher_invites_in_the_api_and_queues_the_activation_link_for_the_invitee()
        {
            await InviteAsync(Form(issuers: [IssuerA1, IssuerA2], resetMfa: true));

            var done = await DispatchAsync();

            Assert.Equal(1, done.Data);
            var call = Assert.Single(_api.InvitationCalls);
            Assert.Equal(("ana@alfa.pe", "Ana Pérez", true), (call.Email, call.Name, call.ResetMfa));
            Assert.Equal([501L, 502L], call.TenantIds.Order());
            // El actor es quien pidió la invitación, no "sistema".
            Assert.Equal(_user.UserId, call.Actor);

            var access = await AccessAsync(1);
            Assert.Equal((RemoteActionStatus.Done, 500L, 1, false), (access.Status, access.ApiUserId, access.InvitationCount, access.ResetMfa));
            Assert.NotNull(access.InvitedOnUtc);
            Assert.NotNull(access.InvitationExpiresOnUtc);

            var notice = Assert.Single(await NotificationsAsync());
            Assert.Equal((NotificationType.PortalInvitation, NotificationStatus.Pending, "ana@alfa.pe"), (notice.Type, notice.Status, notice.ToEmail));
            Assert.Equal("portal-invite:1:1", notice.DedupKey);
            Assert.Contains($"https://portal.midominio.pe/activar?token={Token}", notice.Body);
            Assert.Contains("ALFA &lt;SAC&gt;", notice.Body);
            Assert.Contains("ALFA DOS (RUC 20100000002)", notice.Body);
        }

        [Fact]
        public async Task The_invitation_goes_to_the_invitee_even_if_the_customer_has_no_contact_email()
        {
            await InviteAsync(Form("luis@beta.pe", Beta, [IssuerB1]));

            await DispatchAsync();

            var notice = Assert.Single(await NotificationsAsync());
            Assert.Equal((NotificationStatus.Pending, "luis@beta.pe"), (notice.Status, notice.ToEmail));
        }

        [Fact]
        public async Task The_token_is_not_stored_anywhere_but_the_notice_and_never_reaches_the_audit_trail()
        {
            await InvitedAsync();

            await using var db = _database.CreateContext(_user);
            var audits = await db.AuditTrails.AsNoTracking().ToListAsync();
            Assert.Contains(audits, a => a.TableName == nameof(Notification));
            Assert.DoesNotContain(audits, a => (a.NewValues ?? string.Empty).Contains(Token) || (a.OldValues ?? string.Empty).Contains(Token));
            var access = await AccessAsync(1);
            Assert.DoesNotContain(Token, access.LastError ?? string.Empty);
        }

        [Fact]
        public void The_audit_masks_the_body_of_a_portal_invitation_only()
        {
            Assert.True(AuditableContext.IsSensitiveColumn(
                new Notification { Type = NotificationType.PortalInvitation, CustomerId = 1, DedupKey = "k", Subject = "s", Body = "b" }, nameof(Notification.Body)));
            Assert.False(AuditableContext.IsSensitiveColumn(
                new Notification { Type = NotificationType.DueSoon, CustomerId = 1, DedupKey = "k", Subject = "s", Body = "b" }, nameof(Notification.Body)));
        }

        [Fact]
        public async Task Once_the_email_leaves_the_link_is_replaced_by_a_text_without_it()
        {
            await InvitedAsync();
            Assert.Contains(Token, (await NotificationsAsync()).Single().Body);

            await SendMailAsync();

            var sent = Assert.Single(_mail.Sent);
            Assert.Contains(Token, sent.Body);
            var notice = Assert.Single(await NotificationsAsync());
            Assert.Equal(NotificationStatus.Sent, notice.Status);
            Assert.Equal(NoticeTemplates.PortalInvitationScrubbedBody, notice.Body);
            Assert.DoesNotContain(Token, notice.Body);
        }

        [Fact]
        public async Task An_invitation_that_cannot_be_sent_does_not_keep_the_link_either()
        {
            await InvitedAsync();
            _mail.FailWith = new InvalidOperationException("smtp caído");
            for (var i = 0; i < NotificationRetryPolicy.MaxAttempts; i++)
            {
                await SendMailAsync();
                _clock.Advance(TimeSpan.FromHours(7));
                if (i == 0)
                {
                    // Mientras reintenta todavía conserva el enlace (la persona aún puede recibirlo).
                    Assert.Contains(Token, (await NotificationsAsync()).Single().Body);
                }
            }

            var notice = Assert.Single(await NotificationsAsync());
            Assert.Equal(NotificationStatus.Abandoned, notice.Status);
            Assert.DoesNotContain(Token, notice.Body);

            // Y no se puede reintentar ese aviso: hay que reinvitar desde Portal.
            var retry = await SendAsync(uow => new RetryNotificationCommandHandler(uow, _clock, new TestLocalizer())
                .Handle(new RetryNotificationCommand { Id = notice.Id }, CancellationToken.None));
            Assert.False(retry.Succeeded);
            Assert.Equal(RetryNotificationCommandHandler.PortalInvitationMessage, retry.Messages.Single());
        }

        [Fact]
        public async Task Each_invitation_queues_its_own_notice()
        {
            var id = await InvitedAsync();
            _api.NextToken = "tok-secreto-2";
            await InviteAsync(Form());
            await DispatchAsync();

            var notices = await NotificationsAsync();
            Assert.Equal(["portal-invite:1:1", "portal-invite:1:2"], notices.Select(n => n.DedupKey));
            Assert.Contains("tok-secreto-2", notices[1].Body);
            Assert.Equal(2, (await AccessAsync(id)).InvitationCount);
        }

        [Fact]
        public async Task Without_a_public_https_address_the_invitation_waits_and_calls_nothing()
        {
            _settings.PublicUrl = null;
            await InviteAsync(Form());

            var done = await DispatchAsync();

            Assert.Equal(0, done.Data);
            Assert.Empty(_api.InvitationCalls);
            var access = await AccessAsync(1);
            Assert.Equal((RemoteActionStatus.Failed, 1, PortalRules.NotConfiguredMessage), (access.Status, access.Attempts, access.LastError));

            _settings.PublicUrl = "https://portal.midominio.pe";
            _clock.Advance(TimeSpan.FromMinutes(2));
            Assert.Equal(1, (await DispatchAsync()).Data);
            Assert.Equal(RemoteActionStatus.Done, (await AccessAsync(1)).Status);
        }

        [Fact]
        public async Task A_retryable_api_failure_is_retried_later_and_a_final_one_is_abandoned()
        {
            _api.FailInvitationWith = ApiSunatCallResult<ApiPortalInvitation>.Fail(503, "No se pudo conectar con la API SUNAT.", true);
            await InviteAsync(Form());

            Assert.Equal(0, (await DispatchAsync()).Data);
            var waiting = await AccessAsync(1);
            Assert.Equal((RemoteActionStatus.Failed, 1), (waiting.Status, waiting.Attempts));
            Assert.True(waiting.NextAttemptOnUtc > _clock.NowUtc);
            Assert.Empty(await NotificationsAsync());

            // Antes de su hora no se vuelve a tocar.
            await DispatchAsync();
            Assert.Equal(1, (await AccessAsync(1)).Attempts);

            _api.FailInvitationWith = ApiSunatCallResult<ApiPortalInvitation>.Fail(422, "El correo ya es usuario de otra cuenta.", false);
            _clock.Advance(TimeSpan.FromMinutes(2));
            await DispatchAsync();
            var abandoned = await AccessAsync(1);
            Assert.Equal((RemoteActionStatus.Abandoned, "El correo ya es usuario de otra cuenta."), (abandoned.Status, abandoned.LastError));
        }

        [Fact]
        public async Task Retrying_returns_a_failed_access_to_pending_and_only_those()
        {
            _api.FailInvitationWith = ApiSunatCallResult<ApiPortalInvitation>.Fail(422, "rechazada", false);
            var id = (await InviteAsync(Form())).Data;
            Assert.False((await RetryAsync(id)).Succeeded);   // aún Pendiente
            await DispatchAsync();
            Assert.Equal(RemoteActionStatus.Abandoned, (await AccessAsync(id)).Status);

            _api.FailInvitationWith = null;
            Assert.True((await RetryAsync(id)).Succeeded);
            var access = await AccessAsync(id);
            Assert.Equal((RemoteActionStatus.Pending, 0), (access.Status, access.Attempts));
            Assert.Equal(1, (await DispatchAsync()).Data);
            Assert.Equal(RemoteActionStatus.Done, (await AccessAsync(id)).Status);
            Assert.Equal(RetryPortalAccessCommandHandler.NotFoundMessage, (await RetryAsync(99)).Messages.Single());
        }

        [Fact]
        public async Task An_access_being_sent_is_taken_once_and_retaken_when_its_lease_expires()
        {
            await InviteAsync(Form());
            await _database.SeedAsync(db =>
            {
                var a = db.Set<PortalAccess>().Single();
                a.Status = RemoteActionStatus.Sending;
                a.LockedUntilUtc = _clock.NowUtc.AddMinutes(3);
            });

            Assert.Equal(0, (await DispatchAsync()).Data);
            Assert.Empty(_api.InvitationCalls);
            Assert.Equal(InviteToPortalCommandHandler.BusyMessage, (await InviteAsync(Form())).Messages.Single());

            _clock.Advance(TimeSpan.FromMinutes(6));
            Assert.Equal(1, (await DispatchAsync()).Data);
            Assert.Single(_api.InvitationCalls);
        }

        [Fact]
        public async Task An_access_whose_issuers_were_deleted_is_abandoned_with_a_clear_message()
        {
            await InviteAsync(Form());
            await _database.SeedAsync(db => db.Set<ApiSunatIssuer>().Single(i => i.Id == IssuerA1).IsDeleted = true);

            await DispatchAsync();

            var access = await AccessAsync(1);
            Assert.Equal((RemoteActionStatus.Abandoned, DispatchPortalAccessesCommandHandler.NoIssuersMessage), (access.Status, access.LastError));
            Assert.Empty(_api.InvitationCalls);
        }

        // ------------------------------------------------------------------ habilitar / deshabilitar

        [Fact]
        public async Task Disabling_an_invited_person_tells_the_api_and_enabling_brings_them_back()
        {
            var id = await InvitedAsync();

            var off = await SetEnabledAsync(id, false);
            Assert.Equal(SetPortalAccessEnabledCommandHandler.DisabledMessage, off.Messages.Single());
            Assert.Equal((false, PortalWorkType.SetState, RemoteActionStatus.Pending),
                ((await AccessAsync(id)).IsEnabled, (await AccessAsync(id)).WorkType, (await AccessAsync(id)).Status));
            Assert.Empty(_api.PortalStateCalls);

            await DispatchAsync();
            Assert.Equal((500L, false), (_api.PortalStateCalls.Single().UserId, _api.PortalStateCalls.Single().Active));
            Assert.Equal(RemoteActionStatus.Done, (await AccessAsync(id)).Status);

            await SetEnabledAsync(id, true);
            await DispatchAsync();
            Assert.Equal([(500L, false), (500L, true)], _api.PortalStateCalls.Select(c => (c.UserId, c.Active)));
            // Habilitar o deshabilitar no manda otra invitación ni otro correo.
            Assert.Single(_api.InvitationCalls);
            Assert.Single(await NotificationsAsync());
        }

        [Fact]
        public async Task A_state_change_that_the_api_cannot_apply_now_is_retried()
        {
            var id = await InvitedAsync();
            await SetEnabledAsync(id, false);
            _api.FailPortalStateWith = ApiSunatCallResult<bool>.Fail(503, "No se pudo conectar con la API SUNAT.", true);

            await DispatchAsync();
            var access = await AccessAsync(id);
            Assert.Equal((RemoteActionStatus.Failed, 1), (access.Status, access.Attempts));

            _api.FailPortalStateWith = null;
            await RetryAsync(id);
            await DispatchAsync();
            Assert.Equal(RemoteActionStatus.Done, (await AccessAsync(id)).Status);
        }

        [Fact]
        public async Task Someone_never_invited_in_the_api_cannot_be_enabled_and_disabling_cancels_the_invitation()
        {
            var id = (await InviteAsync(Form())).Data;

            Assert.Equal(SetPortalAccessEnabledCommandHandler.NeverInvitedMessage, (await SetEnabledAsync(id, true)).Messages.Single());

            var off = await SetEnabledAsync(id, false);
            Assert.Equal(SetPortalAccessEnabledCommandHandler.CancelledMessage, off.Messages.Single());
            var access = await AccessAsync(id);
            Assert.Equal((false, RemoteActionStatus.Cancelled), (access.IsEnabled, access.Status));
            Assert.Equal(0, (await DispatchAsync()).Data);
            Assert.Empty(_api.InvitationCalls);
            Assert.Equal(SetPortalAccessEnabledCommandHandler.NotFoundMessage, (await SetEnabledAsync(99, true)).Messages.Single());
        }

        // ------------------------------------------------------------------ listado

        [Fact]
        public async Task The_list_shows_customer_and_issuers_and_filters_by_state_customer_and_text()
        {
            await InvitedAsync(Form("ana@alfa.pe", Alfa, [IssuerA1, IssuerA2]));
            await InviteAsync(Form("luis@beta.pe", Beta, [IssuerB1]));     // sigue Pendiente

            Task<Result<IEnumerable<DS.Platform.Application.DTOs.Portal.Response.PortalAccessResponseDto>>> List(
                RemoteActionStatus? status = null, int? customer = null, string? text = null, bool? enabled = null)
                => SendAsync(uow => new GetAllPortalAccessesPagedQueryHandler(uow, new TestLocalizer()).Handle(new GetAllPortalAccessesPagedQuery
                {
                    Status = status, CustomerId = customer, IsEnabled = enabled,
                    BaseFiltersRequest = new DS.Platform.Shared.Commons.Bases.Request.BaseFiltersRequest { TextFilter = text }
                }, CancellationToken.None));

            var all = await List();
            Assert.Equal(2, all.TotalRecords);
            var ana = all.Data.Single(a => a.Email == "ana@alfa.pe");
            Assert.Equal(("ALFA <SAC>", true, "Hecho"), (ana.CustomerName, ana.Invited, ana.StatusName));
            Assert.Equal(["ALFA DOS", "ALFA UNO"], ana.Issuers.Select(i => i.BusinessName).Order());
            Assert.Equal(["luis@beta.pe"], (await List(RemoteActionStatus.Pending)).Data.Select(a => a.Email));
            Assert.Equal(["ana@alfa.pe"], (await List(customer: Alfa)).Data.Select(a => a.Email));
            Assert.Equal(["luis@beta.pe"], (await List(text: "BETA")).Data.Select(a => a.Email));
            Assert.Equal(2, (await List(enabled: true)).TotalRecords);
            Assert.Equal(0, (await List(enabled: false)).TotalRecords);
        }
    }
}
