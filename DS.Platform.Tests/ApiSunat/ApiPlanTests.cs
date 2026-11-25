using DS.Platform.Application.DTOs.ApiSunat.Request;
using DS.Platform.Application.DTOs.Plans.Request;
using DS.Platform.Application.Features.ApiSunat.Commands.Link;
using DS.Platform.Application.Features.ApiSunat.Common;
using DS.Platform.Application.Features.Plans.Commands.Update;
using DS.Platform.Application.Features.Plans.Validators;
using DS.Platform.Application.Features.RemoteActions.Commands.Dispatch;
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
    /// Plan de la API SUNAT aplicado desde la consola (fase C3): el plan de la suscripción del cliente se refleja en la bandeja de
    /// acciones remotas (mismo guardado), el despachador lo aplica con reintentos y editar el plan reajusta a sus clientes.
    /// </summary>
    public class ApiPlanTests
    {
        private const int Alfa = 1, ErpWithApi = 11, ApiBusiness = 12;
        private const long TenantAlfa = 501;
        private static readonly DateOnly Today = new(2026, 11, 1);

        private readonly TestDatabase _database = new();
        private readonly TestCurrentUserService _user = TestCurrentUserService.User("cobranzas");
        private readonly TestClock _clock = new(Today);
        private readonly FakeApiSunatAdminClient _api = new();

        public ApiPlanTests()
        {
            // La empresa de la API nace en "pro" con el cupo de 1000 (lo que devuelve la fake).
            _api.With(FakeApiSunatAdminClient.Tenant(TenantAlfa, CustomerHandlersTests.RucAlfa, "ALFA SAC"));
            _database.SeedAsync(db =>
            {
                db.Set<Customer>().Add(new Customer { Id = Alfa, Ruc = CustomerHandlersTests.RucAlfa, BusinessName = "ALFA SAC" });
                db.Set<Plan>().AddRange(
                    new Plan
                    {
                        Id = ErpWithApi, Product = PlanProduct.Erp, Code = "ERP-API", Name = "ERP con API", MonthlyPrice = 400m,
                        ApiPlanSlug = "business", MaxDocumentsPerMonth = 2000
                    },
                    new Plan
                    {
                        Id = ApiBusiness, Product = PlanProduct.ApiSunat, Code = "API-BUS", Name = "API business", MonthlyPrice = 100m,
                        ApiPlanSlug = "business"
                    });
            }).GetAwaiter().GetResult();
        }

        private async Task<T> SendAsync<T>(Func<IUnitOfWork<int>, Task<T>> send)
        {
            await using var db = _database.CreateContext(_user);
            return await send(HandlerKit.UnitOfWork(db, _user));
        }

        private Task SubscribeAsync(int planId, PlanProduct product, SubscriptionStatus status = SubscriptionStatus.Active)
            => _database.SeedAsync(db => db.Set<Subscription>().Add(new Subscription
            {
                CustomerId = Alfa, PlanId = planId, Product = product, StartOn = Today.AddDays(-20), BillingDay = 1,
                PaidUntil = Today.AddDays(10), CoveredUntil = Today.AddDays(10), Status = status, IsActive = true
            }));

        private Task<Result<int>> LinkAsync() => SendAsync(uow =>
            new LinkIssuerCommandHandler(uow, _api, _clock, new TestLocalizer()).Handle(new LinkIssuerCommand
            {
                LinkIssuerRequestDto = new LinkIssuerRequestDto { CustomerId = Alfa, TenantId = TenantAlfa }
            }, CancellationToken.None));

        private Task<Result<int>> DispatchAsync() => SendAsync(uow =>
            new DispatchRemoteActionsCommandHandler(uow, _api, _clock).Handle(new DispatchRemoteActionsCommand(), CancellationToken.None));

        private async Task<List<RemoteAction>> PlanActionsAsync()
        {
            await using var db = _database.CreateContext(_user);
            return await db.Set<RemoteAction>().AsNoTracking().Where(a => a.Type == RemoteActionType.ApplyPlan).OrderBy(a => a.Id).ToListAsync();
        }

        private async Task<ApiSunatIssuer> IssuerAsync()
        {
            await using var db = _database.CreateContext(_user);
            return await db.Set<ApiSunatIssuer>().AsNoTracking().SingleAsync();
        }

        private static Subscription Sub(Plan plan, SubscriptionStatus status = SubscriptionStatus.Active)
            => new() { CustomerId = 1, PlanId = plan.Id, Plan = plan, Product = plan.Product, Status = status };

        private static Plan P(int id, string? slug, int? max = null)
            => new() { Id = id, Product = PlanProduct.ApiSunat, Code = $"P{id}", Name = $"P{id}", ApiPlanSlug = slug, MaxDocumentsPerMonth = max };

        [Fact]
        public void The_policy_picks_the_highest_current_api_plan_and_ignores_the_rest()
        {
            Assert.Null(ApiPlanPolicy.For([]));
            // Un plan del ERP sin API incluida no dice nada de la API.
            Assert.Null(ApiPlanPolicy.For([Sub(P(1, null))]));
            // Una suscripción suspendida o cancelada no cuenta: la suspensión bloquea la emisión pero no degrada el plan.
            Assert.Null(ApiPlanPolicy.For([Sub(P(1, "business"), SubscriptionStatus.Suspended), Sub(P(2, "pro"), SubscriptionStatus.Cancelled)]));

            var best = ApiPlanPolicy.For([Sub(P(1, "free", 100)), Sub(P(2, "business", 5000)), Sub(P(3, "pro", 1000))])!;
            Assert.Equal(("business", 5000), (best.Slug, best.MaxDocuments));

            // A igual nivel manda el cupo mayor, y "sin cupo" (el del plan) vale más que cualquier número.
            Assert.Equal(7000, ApiPlanPolicy.For([Sub(P(1, "pro", 1000)), Sub(P(2, "pro", 7000))])!.MaxDocuments);
            Assert.Null(ApiPlanPolicy.For([Sub(P(1, "pro", 1000)), Sub(P(2, "pro", null))])!.MaxDocuments);
        }

        [Fact]
        public void Applied_means_the_api_has_the_plan_and_the_requested_quota()
        {
            var issuer = new ApiSunatIssuer { CustomerId = 1, TenantId = 1, Ruc = "20100000001", BusinessName = "X", ApiPlan = "pro", MaxDocumentsMonth = 1000 };
            Assert.True(ApiPlanPolicy.IsApplied(issuer, new ApiPlanPolicy.Desired("pro", null)));
            Assert.True(ApiPlanPolicy.IsApplied(issuer, new ApiPlanPolicy.Desired("PRO", 1000)));
            Assert.False(ApiPlanPolicy.IsApplied(issuer, new ApiPlanPolicy.Desired("pro", 2000)));
            Assert.False(ApiPlanPolicy.IsApplied(issuer, new ApiPlanPolicy.Desired("business", null)));
        }

        [Fact]
        public async Task Linking_a_customer_queues_its_plan_and_the_dispatcher_applies_it_once()
        {
            await SubscribeAsync(ErpWithApi, PlanProduct.Erp);
            var issuerId = (await LinkAsync()).Data;

            var queued = Assert.Single(await PlanActionsAsync());
            Assert.Equal(("business", 2000, RemoteActionStatus.Pending), (queued.PlanSlug, queued.PlanMaxDocuments, queued.Status));
            var before = await IssuerAsync();
            Assert.Equal(("business", 2000), (before.PlanDesiredSlug, before.PlanDesiredMaxDocuments));

            Assert.Equal(1, (await DispatchAsync()).Data);
            Assert.Equal(1, (await DispatchAsync()).Succeeded ? _api.PlanCalls.Count : -1);

            var call = Assert.Single(_api.PlanCalls);
            Assert.Equal((TenantAlfa, "business", (int?)2000), (call.TenantId, call.Plan, call.MaxDocuments));
            var issuer = await IssuerAsync();
            Assert.Equal(issuerId, issuer.Id);
            Assert.Equal(("business", 2000), (issuer.ApiPlan, issuer.MaxDocumentsMonth));
            Assert.Equal(RemoteActionStatus.Done, Assert.Single(await PlanActionsAsync()).Status);
        }

        [Fact]
        public async Task A_plan_the_api_already_has_queues_nothing()
        {
            // La empresa ya está en "pro" con 1000; el plan pide "pro" con el cupo propio del plan.
            await _database.SeedAsync(db => db.Set<Plan>().Add(new Plan
            {
                Id = 13, Product = PlanProduct.Erp, Code = "ERP-PRO", Name = "ERP pro", MonthlyPrice = 1m, ApiPlanSlug = "pro"
            }));
            await SubscribeAsync(13, PlanProduct.Erp);
            await LinkAsync();

            Assert.Empty(await PlanActionsAsync());
        }

        [Fact]
        public async Task Editing_the_plan_re_adjusts_its_customers_and_replaces_the_pending_change()
        {
            await SubscribeAsync(ErpWithApi, PlanProduct.Erp);
            await LinkAsync();
            await DispatchAsync();

            // Se cambia el cupo del plan de 2000 a 3000: el cliente recibe una acción nueva.
            var form = new UpdatePlanRequestDto
            {
                Id = ErpWithApi, Product = PlanProduct.Erp, Code = "ERP-API", Name = "ERP con API", MonthlyPrice = 400m,
                ApiPlanSlug = "business", MaxDocumentsPerMonth = 3000
            };
            Assert.True((await SendAsync(uow => new UpdatePlanCommandHandler(uow, _clock, new TestLocalizer())
                .Handle(new UpdatePlanCommand { UpdatePlanRequestDto = form }, CancellationToken.None))).Succeeded);

            var actions = await PlanActionsAsync();
            Assert.Equal(2, actions.Count);
            Assert.Equal(3000, actions[1].PlanMaxDocuments);

            // Y antes de enviarse, otro cambio la reemplaza: solo la última se aplica.
            form.MaxDocumentsPerMonth = 4000;
            Assert.True((await SendAsync(uow => new UpdatePlanCommandHandler(uow, _clock, new TestLocalizer())
                .Handle(new UpdatePlanCommand { UpdatePlanRequestDto = form }, CancellationToken.None))).Succeeded);
            actions = await PlanActionsAsync();
            Assert.Equal(3, actions.Count);
            Assert.Equal(RemoteActionStatus.Cancelled, actions[1].Status);

            await DispatchAsync();
            Assert.Equal(4000, (await IssuerAsync()).MaxDocumentsMonth);
            Assert.Equal(2, _api.PlanCalls.Count);   // la primera (2000) y la última (4000); la de 3000 nunca salió
        }

        [Fact]
        public async Task A_failed_plan_call_is_retried_and_leaves_the_suspension_untouched()
        {
            await SubscribeAsync(ErpWithApi, PlanProduct.Erp);
            await LinkAsync();
            _api.FailPlanWith = ApiSunatCallResult<ApiTenantInfo>.Fail(503, "No se pudo conectar con la API SUNAT.", true);

            Assert.Equal(0, (await DispatchAsync()).Data);
            var action = Assert.Single(await PlanActionsAsync());
            Assert.Equal((RemoteActionStatus.Failed, 1), (action.Status, action.Attempts));
            Assert.False((await IssuerAsync()).SuspendApplied);
        }

        [Fact]
        public async Task A_suspended_customer_keeps_its_plan_and_a_plan_change_does_not_cancel_a_suspension()
        {
            await SubscribeAsync(ErpWithApi, PlanProduct.Erp, SubscriptionStatus.Suspended);
            await LinkAsync();

            Assert.Empty(await PlanActionsAsync());   // sin suscripción vigente no se toca el plan
            await using var db = _database.CreateContext(_user);
            var suspend = await db.Set<RemoteAction>().AsNoTracking().SingleAsync(a => a.Type == RemoteActionType.SuspendIssuer);
            Assert.Equal(RemoteActionStatus.Pending, suspend.Status);
        }

        [Fact]
        public async Task The_plan_limits_how_many_issuers_a_customer_can_link()
        {
            await _database.SeedAsync(db => db.Set<Plan>().Add(new Plan
            {
                Id = 14, Product = PlanProduct.Erp, Code = "ERP-1RUC", Name = "ERP 1 RUC", MonthlyPrice = 1m, ApiPlanSlug = "pro", MaxIssuers = 1
            }));
            _api.With(FakeApiSunatAdminClient.Tenant(777, CustomerHandlersTests.RucBeta, "OTRA SAC"));
            await SubscribeAsync(14, PlanProduct.Erp);

            Assert.True((await LinkAsync()).Succeeded);

            var second = await SendAsync(uow => new LinkIssuerCommandHandler(uow, _api, _clock, new TestLocalizer()).Handle(new LinkIssuerCommand
            {
                LinkIssuerRequestDto = new LinkIssuerRequestDto { CustomerId = Alfa, TenantId = 777 }
            }, CancellationToken.None));
            Assert.False(second.Succeeded);
            Assert.Contains(LinkIssuerCommandHandler.IssuerLimitMessage(1), second.Messages);
        }

        [Fact]
        public void The_issuer_limit_is_the_highest_current_one_and_no_limit_wins()
        {
            Assert.Null(ApiPlanPolicy.IssuerLimit([]));
            var one = new Plan { Id = 1, Product = PlanProduct.ApiSunat, Code = "A", Name = "A", ApiPlanSlug = "pro", MaxIssuers = 1 };
            var three = new Plan { Id = 2, Product = PlanProduct.ApiSunat, Code = "B", Name = "B", ApiPlanSlug = "pro", MaxIssuers = 3 };
            var unlimited = new Plan { Id = 3, Product = PlanProduct.ApiSunat, Code = "C", Name = "C", ApiPlanSlug = "pro" };
            Assert.Equal(3, ApiPlanPolicy.IssuerLimit([Sub(one), Sub(three)]));
            Assert.Null(ApiPlanPolicy.IssuerLimit([Sub(one), Sub(unlimited)]));
            Assert.Equal(1, ApiPlanPolicy.IssuerLimit([Sub(one), Sub(three, SubscriptionStatus.Suspended)]));
        }

        [Theory]
        [InlineData("free", true)]
        [InlineData("PRO", true)]
        [InlineData("business", true)]
        [InlineData("enterprise", false)]
        public void The_form_accepts_only_the_api_plans(string slug, bool valid)
        {
            var dto = new PlanRequestDto { Product = PlanProduct.ApiSunat, Code = "API-X", Name = "API", ApiPlanSlug = slug, MaxDocumentsPerMonth = 100 };
            Assert.Equal(valid, new PlanRequestValidator().Validate(dto).IsValid);
        }

        [Fact]
        public void An_api_plan_must_say_which_api_plan_applies_and_an_erp_plan_may_leave_it_empty()
        {
            var api = new PlanRequestDto { Product = PlanProduct.ApiSunat, Code = "API-X", Name = "API", MaxDocumentsPerMonth = 100 };
            Assert.False(new PlanRequestValidator().Validate(api).IsValid);

            var erp = new PlanRequestDto { Product = PlanProduct.Erp, Code = "ERP-X", Name = "ERP", MaxUsers = 5 };
            Assert.True(new PlanRequestValidator().Validate(erp).IsValid);
            // Los límites de la API en un plan del ERP solo valen si lleva API incluida.
            erp.MaxDocumentsPerMonth = 500;
            Assert.False(new PlanRequestValidator().Validate(erp).IsValid);
            erp.ApiPlanSlug = "pro";
            Assert.True(new PlanRequestValidator().Validate(erp).IsValid);
        }
    }
}
