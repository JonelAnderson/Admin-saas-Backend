using DS.Platform.Application.DTOs.Subscriptions.Request;
using DS.Platform.Application.Features.Plans.Commands.Delete;
using DS.Platform.Application.Features.Subscriptions.Commands.Cancel;
using DS.Platform.Application.Features.Subscriptions.Commands.Create;
using DS.Platform.Application.Features.Subscriptions.Commands.Evaluate;
using DS.Platform.Application.Features.Subscriptions.Commands.Extend;
using DS.Platform.Application.Features.Subscriptions.Commands.Reactivate;
using DS.Platform.Application.Features.Subscriptions.Commands.Suspend;
using DS.Platform.Application.Features.Subscriptions.Commands.Update;
using DS.Platform.Application.Features.Subscriptions.Queries.GetAllPaged;
using DS.Platform.Domain.Entities.Billing;
using DS.Platform.Domain.Entities.Customers;
using DS.Platform.Domain.Enums;
using DS.Platform.Shared.Wrapper;
using DS.Platform.Tests.Customers;
using DS.Platform.Tests.Support;
using Microsoft.EntityFrameworkCore;

namespace DS.Platform.Tests.Subscriptions
{
    /// <summary>
    /// Suscripciones (qué cliente tiene qué plan): alta con prueba solo la primera vez, una por producto, prórroga, suspensión
    /// manual, reactivación, cancelación, cambio de plan y el recálculo diario con el reloj de Lima.
    /// </summary>
    public class SubscriptionHandlersTests
    {
        private const int Alfa = 1, Beta = 2, ErpBasic = 10, ErpPro = 11, ApiPlan = 20, ErpNoTrial = 12;
        private static readonly DateOnly Today = new(2026, 11, 1);

        private readonly TestDatabase _database = new();
        private readonly TestCurrentUserService _user = TestCurrentUserService.User("cobranzas");
        private readonly TestClock _clock = new(Today);

        public SubscriptionHandlersTests()
        {
            _database.SeedAsync(db =>
            {
                db.Set<Customer>().AddRange(
                    new Customer { Id = Alfa, Ruc = CustomerHandlersTests.RucAlfa, BusinessName = "ALFA SAC" },
                    new Customer { Id = Beta, Ruc = CustomerHandlersTests.RucBeta, BusinessName = "BETA SAC" });
                db.Set<Plan>().AddRange(
                    new Plan { Id = ErpBasic, Product = PlanProduct.Erp, Code = "ERP-BASICO", Name = "ERP Básico", MonthlyPrice = 350m, TrialDays = 14 },
                    new Plan { Id = ErpPro, Product = PlanProduct.Erp, Code = "ERP-PRO", Name = "ERP Pro", MonthlyPrice = 600m, YearlyPrice = 6000m },
                    new Plan { Id = ErpNoTrial, Product = PlanProduct.Erp, Code = "ERP-X", Name = "ERP X", MonthlyPrice = 100m },
                    new Plan { Id = ApiPlan, Product = PlanProduct.ApiSunat, Code = "API-1000", Name = "API 1000", MonthlyPrice = 60m, TrialDays = 7 });
            }).GetAwaiter().GetResult();
        }

        private async Task<Result<int>> SendAsync(Func<Application.Interfaces.Repositories.IUnitOfWork<int>, Task<Result<int>>> send)
        {
            await using var db = _database.CreateContext(_user);
            return await send(HandlerKit.UnitOfWork(db, _user));
        }

        private Task<Result<int>> CreateAsync(int customer, int plan, BillingCycle cycle = BillingCycle.Monthly, decimal? price = null)
            => SendAsync(uow => new CreateSubscriptionCommandHandler(uow, _clock, new TestLocalizer()).Handle(new CreateSubscriptionCommand
            {
                CreateSubscriptionRequestDto = new CreateSubscriptionRequestDto { CustomerId = customer, PlanId = plan, BillingCycle = cycle, Price = price }
            }, CancellationToken.None));

        private Task<Result<int>> ReasonAsync(string action, int id, string reason = "motivo") => SendAsync(uow =>
        {
            var dto = new SubscriptionReasonRequestDto { Id = id, Reason = reason };
            return action switch
            {
                "suspend" => new SuspendSubscriptionCommandHandler(uow, _clock, new TestLocalizer()).Handle(new SuspendSubscriptionCommand { SubscriptionReasonRequestDto = dto }, CancellationToken.None),
                "reactivate" => new ReactivateSubscriptionCommandHandler(uow, _clock, new TestLocalizer()).Handle(new ReactivateSubscriptionCommand { SubscriptionReasonRequestDto = dto }, CancellationToken.None),
                _ => new CancelSubscriptionCommandHandler(uow, _clock, new TestLocalizer()).Handle(new CancelSubscriptionCommand { SubscriptionReasonRequestDto = dto }, CancellationToken.None)
            };
        });

        private Task<Result<int>> ExtendAsync(int id, DateOnly paidUntil) => SendAsync(uow =>
            new ExtendSubscriptionCommandHandler(uow, _clock, new TestLocalizer()).Handle(new ExtendSubscriptionCommand
            {
                ExtendSubscriptionRequestDto = new ExtendSubscriptionRequestDto { Id = id, PaidUntil = paidUntil, Reason = "pago por transferencia pendiente" }
            }, CancellationToken.None));

        private async Task<Subscription> LoadAsync(int id)
        {
            await using var db = _database.CreateContext(_user);
            return await db.Set<Subscription>().AsNoTracking().SingleAsync(s => s.Id == id);
        }

        private static int Ok(Result<int> result)
        {
            Assert.True(result.Succeeded, string.Join(" ", result.Messages));
            return result.Data;
        }

        [Fact]
        public async Task A_customer_subscribes_to_a_plan_with_its_trial_price_and_billing_day()
        {
            var id = Ok(await CreateAsync(Alfa, ErpBasic));

            var s = await LoadAsync(id);
            Assert.Equal((Alfa, ErpBasic, PlanProduct.Erp, 350m, "PEN"), (s.CustomerId, s.PlanId, s.Product, s.Price, s.Currency));
            Assert.Equal((Today, Today.AddDays(13), 1), (s.StartOn, s.TrialEndsOn!.Value, s.BillingDay));
            Assert.Equal((SubscriptionStatus.Trial, Today.AddDays(13 + 5 + 1)), (s.Status, s.SuspendOn!.Value));
            Assert.Equal("cobranzas", s.CreatedBy);
        }

        [Fact]
        public async Task One_open_subscription_per_product_but_the_customer_can_have_both_products()
        {
            Ok(await CreateAsync(Alfa, ErpBasic));

            var second = await CreateAsync(Alfa, ErpPro);
            Assert.False(second.Succeeded);
            Assert.Contains(CreateSubscriptionCommandHandler.AlreadySubscribedMessage(PlanProduct.Erp), second.Messages);

            Ok(await CreateAsync(Alfa, ApiPlan));
        }

        [Fact]
        public async Task The_free_trial_is_given_only_once_per_customer_and_product()
        {
            var first = Ok(await CreateAsync(Alfa, ErpBasic));
            Ok(await ReasonAsync("cancel", first, "no le convenció"));

            var again = Ok(await CreateAsync(Alfa, ErpBasic));
            var s = await LoadAsync(again);
            Assert.Null(s.TrialEndsOn);
            Assert.Equal(SubscriptionStatus.Grace, s.Status); // sin prueba y sin pago: en gracia hasta pagar

            // Otro cliente sí recibe su prueba.
            Assert.NotNull((await LoadAsync(Ok(await CreateAsync(Beta, ErpBasic)))).TrialEndsOn);
        }

        [Fact]
        public async Task Yearly_needs_a_yearly_price_and_inactive_customers_or_plans_are_rejected()
        {
            Assert.False((await CreateAsync(Alfa, ErpBasic, BillingCycle.Yearly)).Succeeded);
            var yearly = Ok(await CreateAsync(Alfa, ErpPro, BillingCycle.Yearly));
            Assert.Equal(6000m, (await LoadAsync(yearly)).Price);

            await _database.SeedAsync(db =>
            {
                db.Set<Customer>().Single(c => c.Id == Beta).IsActive = false;
                db.Set<Plan>().Single(p => p.Id == ApiPlan).IsActive = false;
            });
            Assert.Contains(CreateSubscriptionCommandHandler.CustomerInactiveMessage, (await CreateAsync(Beta, ErpPro)).Messages);
            Assert.Contains(CreateSubscriptionCommandHandler.PlanInactiveMessage, (await CreateAsync(Alfa, ApiPlan)).Messages);
        }

        [Fact]
        public async Task The_daily_evaluation_moves_trial_to_grace_and_to_suspended_and_an_extension_reactivates()
        {
            var id = Ok(await CreateAsync(Alfa, ErpBasic)); // prueba hasta el 14/11, gracia hasta el 19/11

            async Task<int> EvaluateAsync(DateOnly day)
            {
                _clock.SetLimaDate(day);
                return Ok(await SendAsync(uow => new EvaluateSubscriptionsCommandHandler(uow, _clock).Handle(new EvaluateSubscriptionsCommand(), CancellationToken.None)));
            }

            Assert.Equal(0, await EvaluateAsync(new DateOnly(2026, 11, 10)));
            Assert.Equal(1, await EvaluateAsync(new DateOnly(2026, 11, 15)));
            Assert.Equal(SubscriptionStatus.Grace, (await LoadAsync(id)).Status);
            Assert.Equal(1, await EvaluateAsync(new DateOnly(2026, 11, 20)));
            Assert.Equal(SubscriptionStatus.Suspended, (await LoadAsync(id)).Status);

            // Una fecha que no es posterior a hoy se rechaza; la prórroga reactiva.
            Assert.Contains(ExtendSubscriptionCommandHandler.NotLaterMessage, (await ExtendAsync(id, new DateOnly(2026, 11, 19))).Messages);
            Ok(await ExtendAsync(id, new DateOnly(2026, 12, 20)));
            var s = await LoadAsync(id);
            Assert.Equal((SubscriptionStatus.Active, new DateOnly(2026, 12, 20)), (s.Status, s.PaidUntil!.Value));
            Assert.StartsWith("Prórroga hasta 20/12/2026", s.StatusReason);
        }

        [Fact]
        public async Task Manual_suspension_and_reactivation_and_an_unpaid_one_stays_suspended()
        {
            var id = Ok(await CreateAsync(Alfa, ErpBasic));

            Ok(await ReasonAsync("suspend", id, "uso indebido"));
            Assert.Equal((SubscriptionStatus.Suspended, true), ((await LoadAsync(id)).Status, (await LoadAsync(id)).SuspendedManually));
            Assert.False((await ReasonAsync("suspend", id)).Succeeded);

            Ok(await ReasonAsync("reactivate", id, "aclarado"));
            Assert.Equal(SubscriptionStatus.Trial, (await LoadAsync(id)).Status);
            Assert.False((await ReasonAsync("reactivate", id)).Succeeded);

            // Vencida y suspendida a mano: al reactivar sigue suspendida por falta de pago (y el mensaje lo dice).
            Ok(await ReasonAsync("suspend", id));
            _clock.SetLimaDate(new DateOnly(2026, 12, 1));
            var reactivated = await ReasonAsync("reactivate", id);
            Assert.True(reactivated.Succeeded);
            Assert.Contains(ReactivateSubscriptionCommandHandler.StillUnpaidMessage, reactivated.Messages);
            Assert.Equal(SubscriptionStatus.Suspended, (await LoadAsync(id)).Status);
        }

        [Fact]
        public async Task A_cancelled_subscription_is_final()
        {
            var id = Ok(await CreateAsync(Alfa, ErpBasic));
            Ok(await ReasonAsync("cancel", id, "cierra su negocio"));

            var s = await LoadAsync(id);
            Assert.Equal((SubscriptionStatus.Cancelled, Today, (DateOnly?)null), (s.Status, s.CancelledOn!.Value, s.SuspendOn));
            Assert.Contains(CreateSubscriptionCommandHandler.CancelledMessage, (await ExtendAsync(id, new DateOnly(2026, 12, 31))).Messages);
            Assert.Contains(CreateSubscriptionCommandHandler.CancelledMessage, (await ReasonAsync("suspend", id)).Messages);
        }

        [Fact]
        public async Task Changing_plan_keeps_the_product_and_a_plan_in_use_cannot_be_deleted()
        {
            var id = Ok(await CreateAsync(Alfa, ErpBasic));

            Task<Result<int>> UpdateAsync(int plan) => SendAsync(uow => new UpdateSubscriptionCommandHandler(uow, _clock, new TestLocalizer())
                .Handle(new UpdateSubscriptionCommand
                {
                    UpdateSubscriptionRequestDto = new UpdateSubscriptionRequestDto { Id = id, PlanId = plan, Price = 500m, BillingDay = 15, GraceDays = 3 }
                }, CancellationToken.None));

            Assert.Contains(UpdateSubscriptionCommandHandler.OtherProductMessage, (await UpdateAsync(ApiPlan)).Messages);
            Ok(await UpdateAsync(ErpPro));
            var s = await LoadAsync(id);
            Assert.Equal((ErpPro, 500m, 15, 3), (s.PlanId, s.Price, s.BillingDay, s.GraceDays));
            Assert.Equal(Today.AddDays(13 + 3 + 1), s.SuspendOn); // la gracia nueva se recalcula

            var delete = await SendAsync(uow => new DeletePlanCommandHandler(uow, new TestLocalizer()).Handle(new DeletePlanCommand { Id = ErpPro }, CancellationToken.None));
            Assert.Contains(DeletePlanCommandHandler.InUseMessage, delete.Messages);
        }

        [Fact]
        public async Task The_list_shows_customer_and_plan_and_filters_by_customer_status_and_product()
        {
            Ok(await CreateAsync(Alfa, ErpBasic));
            Ok(await CreateAsync(Alfa, ApiPlan));
            var beta = Ok(await CreateAsync(Beta, ErpNoTrial));

            await using var db = _database.CreateContext(_user);
            var handler = new GetAllSubscriptionsPagedQueryHandler(HandlerKit.UnitOfWork(db, _user), new TestLocalizer());

            var ofAlfa = await handler.Handle(new GetAllSubscriptionsPagedQuery { CustomerId = Alfa }, CancellationToken.None);
            Assert.Equal(2, ofAlfa.TotalRecords);
            Assert.All(ofAlfa.Data, s => Assert.Equal(("ALFA SAC", CustomerHandlersTests.RucAlfa), (s.CustomerName, s.CustomerRuc)));

            var grace = await handler.Handle(new GetAllSubscriptionsPagedQuery { Status = SubscriptionStatus.Grace }, CancellationToken.None);
            var row = Assert.Single(grace.Data);
            Assert.Equal((beta, "ERP X", "ERP", "En gracia", "Mensual"), (row.Id, row.PlanName, row.ProductName, row.StatusName, row.BillingCycleName));

            var api = await handler.Handle(new GetAllSubscriptionsPagedQuery { Product = PlanProduct.ApiSunat }, CancellationToken.None);
            Assert.Equal(["API 1000"], api.Data.Select(s => s.PlanName));
        }
    }
}
