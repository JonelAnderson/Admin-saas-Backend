using DS.Platform.Application.DTOs.ApiSunat.Request;
using DS.Platform.Application.DTOs.Subscriptions.Request;
using DS.Platform.Application.Features.ApiSunat.Commands.Link;
using DS.Platform.Application.Features.ApiSunat.Commands.Sync;
using DS.Platform.Application.Features.ApiSunat.Commands.Unlink;
using DS.Platform.Application.Features.ApiSunat.Common;
using DS.Platform.Application.Features.ApiSunat.Queries.SearchTenants;
using DS.Platform.Application.Features.RemoteActions.Commands.Dispatch;
using DS.Platform.Application.Features.RemoteActions.Commands.Retry;
using DS.Platform.Application.Features.RemoteActions.Common;
using DS.Platform.Application.Features.Subscriptions.Commands.Evaluate;
using DS.Platform.Application.Features.Subscriptions.Commands.Reactivate;
using DS.Platform.Application.Features.Subscriptions.Commands.Suspend;
using DS.Platform.Application.Interfaces.Repositories;
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

namespace DS.Platform.Tests.ApiSunat
{
    /// <summary>
    /// Corte efectivo en la API SUNAT (fase C3): el estado de las suscripciones del cliente se refleja en la bandeja de acciones
    /// remotas (mismo guardado), el despachador la aplica con reintentos y nunca dos veces, y las empresas emisoras se vinculan
    /// leyendo los datos de la API.
    /// </summary>
    public class ApiSunatTests
    {
        private const int Alfa = 1, Beta = 2, ErpBasic = 10;
        private const long TenantAlfa = 501, TenantBeta = 502;
        private static readonly DateOnly Today = new(2026, 11, 1);

        private readonly TestDatabase _database = new();
        private readonly TestCurrentUserService _user = TestCurrentUserService.User("cobranzas");
        private readonly TestClock _clock = new(Today);
        private readonly FakeApiSunatAdminClient _api = new();

        public ApiSunatTests()
        {
            _api.With(FakeApiSunatAdminClient.Tenant(TenantAlfa, CustomerHandlersTests.RucAlfa, "ALFA SAC"))
                .With(FakeApiSunatAdminClient.Tenant(TenantBeta, CustomerHandlersTests.RucBeta, "BETA SAC"));
            _database.SeedAsync(db =>
            {
                db.Set<Customer>().AddRange(
                    new Customer { Id = Alfa, Ruc = CustomerHandlersTests.RucAlfa, BusinessName = "ALFA SAC" },
                    new Customer { Id = Beta, Ruc = CustomerHandlersTests.RucBeta, BusinessName = "BETA SAC" });
                db.Set<Plan>().Add(new Plan { Id = ErpBasic, Product = PlanProduct.Erp, Code = "ERP-BASICO", Name = "ERP Básico", MonthlyPrice = 350m, TrialDays = 14 });
            }).GetAwaiter().GetResult();
        }

        private async Task<T> SendAsync<T>(Func<IUnitOfWork<int>, Task<T>> send)
        {
            await using var db = _database.CreateContext(_user);
            return await send(HandlerKit.UnitOfWork(db, _user));
        }

        private async Task<int> SubscribeAsync(int customer, SubscriptionStatus status = SubscriptionStatus.Active, DateOnly? paidUntil = null)
        {
            var id = 0;
            await _database.SeedAsync(db =>
            {
                var s = new Subscription
                {
                    CustomerId = customer, PlanId = ErpBasic, Product = PlanProduct.Erp, StartOn = Today.AddDays(-20), BillingDay = 1,
                    PaidUntil = paidUntil ?? Today.AddDays(10), CoveredUntil = paidUntil ?? Today.AddDays(10), Status = status, IsActive = true
                };
                db.Set<Subscription>().Add(s);
                db.SaveChanges();
                id = s.Id;
            });
            return id;
        }

        private Task<Result<int>> LinkAsync(int customer, long tenant) => SendAsync(uow =>
            new LinkIssuerCommandHandler(uow, _api, _clock, new TestLocalizer()).Handle(new LinkIssuerCommand
            {
                LinkIssuerRequestDto = new LinkIssuerRequestDto { CustomerId = customer, TenantId = tenant }
            }, CancellationToken.None));

        private Task<Result<int>> DispatchAsync() => SendAsync(uow =>
            new DispatchRemoteActionsCommandHandler(uow, _api, _clock).Handle(new DispatchRemoteActionsCommand(), CancellationToken.None));

        private Task<Result<int>> SuspendAsync(int subscriptionId) => SendAsync(uow =>
            new SuspendSubscriptionCommandHandler(uow, _clock, new TestLocalizer()).Handle(new SuspendSubscriptionCommand
            {
                SubscriptionReasonRequestDto = new SubscriptionReasonRequestDto { Id = subscriptionId, Reason = "no responde" }
            }, CancellationToken.None));

        private Task<Result<int>> ReactivateAsync(int subscriptionId) => SendAsync(uow =>
            new ReactivateSubscriptionCommandHandler(uow, _clock, new TestLocalizer()).Handle(new ReactivateSubscriptionCommand
            {
                SubscriptionReasonRequestDto = new SubscriptionReasonRequestDto { Id = subscriptionId, Reason = "ya pagó" }
            }, CancellationToken.None));

        private Task<Result<int>> UnlinkAsync(int issuer) => SendAsync(uow =>
            new UnlinkIssuerCommandHandler(uow, new TestLocalizer()).Handle(new UnlinkIssuerCommand { Id = issuer }, CancellationToken.None));

        private async Task<List<RemoteAction>> ActionsAsync()
        {
            await using var db = _database.CreateContext(_user);
            return await db.Set<RemoteAction>().AsNoTracking().OrderBy(a => a.Id).ToListAsync();
        }

        private async Task<ApiSunatIssuer> IssuerAsync(int id)
        {
            await using var db = _database.CreateContext(_user);
            return await db.Set<ApiSunatIssuer>().AsNoTracking().SingleAsync(i => i.Id == id);
        }

        private static int Ok(Result<int> result)
        {
            Assert.True(result.Succeeded, string.Join(" ", result.Messages));
            return result.Data;
        }

        [Fact]
        public void The_policy_suspends_only_when_the_customer_has_subscriptions_and_none_is_current()
        {
            static Subscription Sub(SubscriptionStatus s) => new() { CustomerId = 1, PlanId = 1, Product = PlanProduct.Erp, Status = s };

            Assert.Null(ApiAccessPolicy.ShouldSuspend([]));
            Assert.False(ApiAccessPolicy.ShouldSuspend([Sub(SubscriptionStatus.Trial)]));
            Assert.False(ApiAccessPolicy.ShouldSuspend([Sub(SubscriptionStatus.Grace)]));
            Assert.True(ApiAccessPolicy.ShouldSuspend([Sub(SubscriptionStatus.Suspended)]));
            Assert.True(ApiAccessPolicy.ShouldSuspend([Sub(SubscriptionStatus.Cancelled)]));
            // Una vigente basta (el ERP suspendido, pero la API SUNAT contratada sola y al día).
            Assert.False(ApiAccessPolicy.ShouldSuspend([Sub(SubscriptionStatus.Suspended), Sub(SubscriptionStatus.Active)]));
        }

        [Fact]
        public void The_applied_state_respects_a_manual_panel_suspension()
        {
            var manual = FakeApiSunatAdminClient.Tenant(1, "20100000001", "X", active: false);
            Assert.True(ApiAccessPolicy.AppliedFor(manual, desiredSuspended: true));   // ya no emite: cuenta como suspendida
            Assert.False(ApiAccessPolicy.AppliedFor(manual, desiredSuspended: false)); // la consola no la reactiva
            Assert.True(ApiAccessPolicy.AppliedFor(FakeApiSunatAdminClient.Tenant(1, "20100000001", "X", billingSuspended: true), false)); // sigue suspendida por pago
        }

        [Fact]
        public async Task Linking_reads_the_api_and_a_customer_with_a_current_subscription_keeps_emitting()
        {
            await SubscribeAsync(Alfa);

            var id = Ok(await LinkAsync(Alfa, TenantAlfa));

            var issuer = await IssuerAsync(id);
            Assert.Equal((TenantAlfa, CustomerHandlersTests.RucAlfa, "ALFA SAC", "pro"), (issuer.TenantId, issuer.Ruc, issuer.BusinessName, issuer.ApiPlan));
            Assert.False(issuer.SuspendDesired);
            Assert.Empty(await ActionsAsync());
        }

        [Fact]
        public async Task Linking_a_customer_without_a_current_subscription_queues_the_suspension_in_the_same_save()
        {
            await SubscribeAsync(Alfa, SubscriptionStatus.Suspended);

            var id = Ok(await LinkAsync(Alfa, TenantAlfa));

            Assert.True((await IssuerAsync(id)).SuspendDesired);
            var action = Assert.Single(await ActionsAsync());
            Assert.Equal((RemoteActionType.SuspendIssuer, id, RemoteActionStatus.Pending), (action.Type, action.IssuerId, action.Status));
        }

        [Fact]
        public async Task Linking_rejects_unknown_tenants_duplicates_and_an_unreachable_api()
        {
            Assert.Equal(LinkIssuerCommandHandler.TenantNotFoundMessage, (await LinkAsync(Alfa, 999)).Messages.Single());

            Ok(await LinkAsync(Alfa, TenantAlfa));
            Assert.Equal(LinkIssuerCommandHandler.AlreadyLinkedMessage, (await LinkAsync(Beta, TenantAlfa)).Messages.Single());

            _api.Down = true;
            var down = await LinkAsync(Beta, TenantBeta);
            Assert.False(down.Succeeded);
            Assert.Contains("No se pudo consultar la API SUNAT", down.Messages.Single());
        }

        [Fact]
        public async Task Searching_tenants_marks_the_ones_already_linked()
        {
            Ok(await LinkAsync(Alfa, TenantAlfa));

            var result = await SendAsync(uow => new SearchApiTenantsQueryHandler(uow, _api, new TestLocalizer())
                .Handle(new SearchApiTenantsQuery(), CancellationToken.None));

            Assert.True(result.Succeeded);
            Assert.Equal([(TenantBeta, false), (TenantAlfa, true)], result.Data.Select(t => (t.Id, t.Linked)).ToArray());
        }

        [Fact]
        public async Task Suspending_the_subscription_queues_an_action_the_dispatcher_applies_once_with_the_actor()
        {
            var sub = await SubscribeAsync(Alfa);
            var issuer = Ok(await LinkAsync(Alfa, TenantAlfa));

            Ok(await SuspendAsync(sub));

            var queued = Assert.Single(await ActionsAsync());
            Assert.Equal((RemoteActionType.SuspendIssuer, RemoteActionStatus.Pending), (queued.Type, queued.Status));
            Assert.True((await IssuerAsync(issuer)).SuspendDesired);
            Assert.False((await IssuerAsync(issuer)).SuspendApplied);
            Assert.Empty(_api.StateCalls); // el caso de uso nunca llama a la API

            Assert.Equal(1, Ok(await DispatchAsync()));

            var call = Assert.Single(_api.StateCalls);
            Assert.Equal((TenantAlfa, true, "cobranzas"), (call.TenantId, call.Suspended, call.Actor));
            Assert.True(_api.Tenants[TenantAlfa].BillingSuspended);
            Assert.True((await IssuerAsync(issuer)).SuspendApplied);
            var done = Assert.Single(await ActionsAsync());
            Assert.Equal((RemoteActionStatus.Done, 1), (done.Status, done.Attempts));

            // Otra pasada no repite nada.
            Assert.Equal(0, Ok(await DispatchAsync()));
            Assert.Single(_api.StateCalls);
        }

        [Fact]
        public async Task Reactivating_before_the_dispatch_cancels_the_pending_suspension_and_sends_nothing()
        {
            var sub = await SubscribeAsync(Alfa);
            Ok(await LinkAsync(Alfa, TenantAlfa));
            Ok(await SuspendAsync(sub));

            Ok(await ReactivateAsync(sub));

            var action = Assert.Single(await ActionsAsync());
            Assert.Equal(RemoteActionStatus.Cancelled, action.Status);
            Assert.Equal(0, Ok(await DispatchAsync()));
            Assert.Empty(_api.StateCalls);
        }

        [Fact]
        public async Task Reactivating_after_the_dispatch_queues_the_reactivation()
        {
            var sub = await SubscribeAsync(Alfa);
            var issuer = Ok(await LinkAsync(Alfa, TenantAlfa));
            Ok(await SuspendAsync(sub));
            Ok(await DispatchAsync());

            Ok(await ReactivateAsync(sub));
            Ok(await DispatchAsync());

            Assert.Equal([true, false], _api.StateCalls.Select(c => c.Suspended).ToArray());
            Assert.False(_api.Tenants[TenantAlfa].BillingSuspended);
            var current = await IssuerAsync(issuer);
            Assert.False(current.SuspendDesired);
            Assert.False(current.SuspendApplied);
        }

        [Fact]
        public async Task The_daily_evaluation_queues_the_suspension_when_the_grace_period_ends()
        {
            // Pagada hasta hace 10 días con 5 de gracia: ya debía estar suspendida.
            await SubscribeAsync(Alfa, SubscriptionStatus.Grace, paidUntil: Today.AddDays(-10));
            var issuer = Ok(await LinkAsync(Alfa, TenantAlfa));
            Assert.False((await IssuerAsync(issuer)).SuspendDesired); // en gracia: aún emite

            var changed = Ok(await SendAsync(uow => new EvaluateSubscriptionsCommandHandler(uow, _clock)
                .Handle(new EvaluateSubscriptionsCommand(), CancellationToken.None)));

            Assert.Equal(1, changed);
            Assert.True((await IssuerAsync(issuer)).SuspendDesired);
            var action = Assert.Single(await ActionsAsync());
            Assert.Equal(RemoteActionType.SuspendIssuer, action.Type);
        }

        [Fact]
        public async Task A_retryable_failure_backs_off_and_a_permanent_one_is_abandoned_until_someone_retries()
        {
            var sub = await SubscribeAsync(Alfa);
            Ok(await LinkAsync(Alfa, TenantAlfa));
            Ok(await SuspendAsync(sub));

            _api.FailStateWith = ApiSunatCallResult<ApiTenantInfo>.Fail(503, "No se pudo conectar con la API SUNAT.", retryable: true);
            Assert.Equal(0, Ok(await DispatchAsync()));
            var failed = Assert.Single(await ActionsAsync());
            Assert.Equal((RemoteActionStatus.Failed, 1), (failed.Status, failed.Attempts));
            Assert.Equal(_clock.NowUtc + RemoteActionRetryPolicy.DelayAfter(1), failed.NextAttemptOnUtc);
            Assert.Contains("conectar", failed.LastError);

            // Todavía no toca: no se vuelve a llamar.
            Assert.Equal(0, Ok(await DispatchAsync()));
            Assert.Single(_api.StateCalls);

            // Pasado el tiempo, otro fallo, esta vez definitivo.
            _clock.SetLimaDate(Today.AddDays(1));
            _api.FailStateWith = ApiSunatCallResult<ApiTenantInfo>.Fail(200, "No existe la empresa.", retryable: false);
            Ok(await DispatchAsync());
            var abandoned = Assert.Single(await ActionsAsync());
            Assert.Equal((RemoteActionStatus.Abandoned, 2), (abandoned.Status, abandoned.Attempts));

            // El reintento manual la deja pendiente con los intentos en cero y esta vez funciona.
            _api.FailStateWith = null;
            Ok(await SendAsync(uow => new RetryRemoteActionCommandHandler(uow, _clock, new TestLocalizer())
                .Handle(new RetryRemoteActionCommand { Id = abandoned.Id }, CancellationToken.None)));
            Assert.Equal(1, Ok(await DispatchAsync()));
            Assert.Equal(RemoteActionStatus.Done, Assert.Single(await ActionsAsync()).Status);
        }

        [Fact]
        public async Task Only_failed_or_abandoned_actions_can_be_retried()
        {
            var sub = await SubscribeAsync(Alfa);
            Ok(await LinkAsync(Alfa, TenantAlfa));
            Ok(await SuspendAsync(sub));
            var pending = Assert.Single(await ActionsAsync());

            var result = await SendAsync(uow => new RetryRemoteActionCommandHandler(uow, _clock, new TestLocalizer())
                .Handle(new RetryRemoteActionCommand { Id = pending.Id }, CancellationToken.None));

            Assert.Equal(RetryRemoteActionCommandHandler.NotRetryableMessage, result.Messages.Single());
        }

        [Fact]
        public async Task The_attempts_are_capped_and_a_stuck_sending_action_is_taken_over_after_its_lease()
        {
            Assert.True(RemoteActionRetryPolicy.IsExhausted(RemoteActionRetryPolicy.MaxAttempts));
            Assert.False(RemoteActionRetryPolicy.IsExhausted(RemoteActionRetryPolicy.MaxAttempts - 1));
            Assert.Equal(TimeSpan.FromHours(6), RemoteActionRetryPolicy.DelayAfter(99));

            var sub = await SubscribeAsync(Alfa);
            Ok(await LinkAsync(Alfa, TenantAlfa));
            Ok(await SuspendAsync(sub));
            // Un despachador la tomó y murió: quedó Enviando con el bloqueo vencido.
            await _database.SeedAsync(db =>
            {
                var a = db.Set<RemoteAction>().Single();
                a.Status = RemoteActionStatus.Sending;
                a.LockedUntilUtc = _clock.NowUtc.AddMinutes(-1);
            });

            Assert.Equal(1, Ok(await DispatchAsync()));
        }

        [Fact]
        public async Task A_sending_action_still_inside_its_lease_is_left_alone()
        {
            var sub = await SubscribeAsync(Alfa);
            Ok(await LinkAsync(Alfa, TenantAlfa));
            Ok(await SuspendAsync(sub));
            await _database.SeedAsync(db =>
            {
                var a = db.Set<RemoteAction>().Single();
                a.Status = RemoteActionStatus.Sending;
                a.LockedUntilUtc = _clock.NowUtc.AddMinutes(3);
            });

            Assert.Equal(0, Ok(await DispatchAsync()));
            Assert.Empty(_api.StateCalls);
        }

        [Fact]
        public async Task A_manual_panel_suspension_counts_as_applied_and_is_never_reactivated_by_the_console()
        {
            var sub = await SubscribeAsync(Alfa);
            var issuer = Ok(await LinkAsync(Alfa, TenantAlfa));
            _api.Tenants[TenantAlfa] = _api.Tenants[TenantAlfa] with { IsActive = false }; // alguien la suspendió en el panel
            Ok(await SuspendAsync(sub));
            Ok(await DispatchAsync());
            Assert.True((await IssuerAsync(issuer)).SuspendApplied);

            Ok(await ReactivateAsync(sub));
            Ok(await DispatchAsync());

            Assert.False(_api.Tenants[TenantAlfa].IsActive);                 // sigue suspendida a mano
            Assert.False((await IssuerAsync(issuer)).SuspendApplied);        // la consola ya no la considera suspendida por pago
        }

        [Fact]
        public async Task Sync_refreshes_the_data_and_repairs_a_drift_by_queueing_the_missing_action()
        {
            var sub = await SubscribeAsync(Alfa);
            var issuer = Ok(await LinkAsync(Alfa, TenantAlfa));
            Ok(await SuspendAsync(sub));
            Ok(await DispatchAsync());
            // Alguien reactivó la empresa directamente en la API: la consola sigue queriéndola suspendida.
            _api.Tenants[TenantAlfa] = _api.Tenants[TenantAlfa] with { BillingSuspended = false, BusinessName = "ALFA SAC (RENOMBRADA)" };

            Ok(await SendAsync(uow => new SyncIssuerCommandHandler(uow, _api, _clock, new TestLocalizer())
                .Handle(new SyncIssuerCommand { Id = issuer }, CancellationToken.None)));

            Assert.Equal("ALFA SAC (RENOMBRADA)", (await IssuerAsync(issuer)).BusinessName);
            Assert.Equal(RemoteActionStatus.Pending, (await ActionsAsync()).Last().Status);
            Ok(await DispatchAsync());
            Assert.True(_api.Tenants[TenantAlfa].BillingSuspended);
        }

        [Fact]
        public async Task Unlinking_is_blocked_while_the_console_keeps_the_emission_suspended()
        {
            var sub = await SubscribeAsync(Alfa);
            var issuer = Ok(await LinkAsync(Alfa, TenantAlfa));
            Ok(await SuspendAsync(sub));

            Assert.Equal(UnlinkIssuerCommandHandler.SuspendedMessage, (await UnlinkAsync(issuer)).Messages.Single());

            Ok(await ReactivateAsync(sub));
            Ok(await UnlinkAsync(issuer));

            await using var db = _database.CreateContext(_user);
            Assert.True((await db.Set<ApiSunatIssuer>().SingleAsync(i => i.Id == issuer)).IsDeleted);
        }
    }
}
