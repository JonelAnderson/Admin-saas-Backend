using DS.Platform.Application.DTOs.Payments.Request;
using DS.Platform.Application.DTOs.Subscriptions.Request;
using DS.Platform.Application.Features.Payments.Commands.Register;
using DS.Platform.Application.Features.Payments.Commands.Void;
using DS.Platform.Application.Features.Payments.Queries.GetAllPaged;
using DS.Platform.Application.Features.Subscriptions.Commands.Cancel;
using DS.Platform.Application.Features.Subscriptions.Commands.Create;
using DS.Platform.Application.Features.Subscriptions.Commands.Extend;
using DS.Platform.Application.Interfaces.Repositories;
using DS.Platform.Domain.Entities.Billing;
using DS.Platform.Domain.Entities.Customers;
using DS.Platform.Domain.Enums;
using DS.Platform.Shared.Commons.Bases.Request;
using DS.Platform.Shared.Wrapper;
using DS.Platform.Tests.Customers;
using DS.Platform.Tests.Support;
using Microsoft.EntityFrameworkCore;

namespace DS.Platform.Tests.Payments
{
    /// <summary>
    /// Pagos: el periodo lo calcula el servidor (sin huecos, o desde hoy tras una suspensión larga), extienden "pagado hasta" y
    /// reactivan; la misma operación no se registra dos veces; se anulan con motivo y solo el último (restaura la fecha).
    /// </summary>
    public class PaymentHandlersTests
    {
        private const int Alfa = 1, Erp = 10, ErpTrial = 11, ErpYearly = 12;
        private static readonly DateOnly Nov1 = new(2026, 11, 1);

        private readonly TestDatabase _database = new();
        private readonly TestCurrentUserService _user = TestCurrentUserService.User("cobranzas");
        private readonly TestClock _clock = new(Nov1);

        public PaymentHandlersTests()
        {
            _database.SeedAsync(db =>
            {
                db.Set<Customer>().Add(new Customer { Id = Alfa, Ruc = CustomerHandlersTests.RucAlfa, BusinessName = "ALFA SAC" });
                db.Set<Plan>().AddRange(
                    new Plan { Id = Erp, Product = PlanProduct.Erp, Code = "ERP", Name = "ERP Básico", MonthlyPrice = 350m },
                    new Plan { Id = ErpTrial, Product = PlanProduct.Erp, Code = "ERP-T", Name = "ERP Prueba", MonthlyPrice = 350m, TrialDays = 14 },
                    new Plan { Id = ErpYearly, Product = PlanProduct.Erp, Code = "ERP-A", Name = "ERP Anual", MonthlyPrice = 350m, YearlyPrice = 3500m });
            }).GetAwaiter().GetResult();
        }

        private async Task<Result<int>> SendAsync(Func<IUnitOfWork<int>, Task<Result<int>>> send)
        {
            await using var db = _database.CreateContext(_user);
            return await send(HandlerKit.UnitOfWork(db, _user));
        }

        private static int Ok(Result<int> result)
        {
            Assert.True(result.Succeeded, string.Join(" ", result.Messages));
            return result.Data;
        }

        private async Task<int> SubscribeAsync(int plan, BillingCycle cycle = BillingCycle.Monthly)
            => Ok(await SendAsync(uow => new CreateSubscriptionCommandHandler(uow, _clock, new TestLocalizer()).Handle(new CreateSubscriptionCommand
            {
                CreateSubscriptionRequestDto = new CreateSubscriptionRequestDto { CustomerId = Alfa, PlanId = plan, BillingCycle = cycle }
            }, CancellationToken.None)));

        private Task<Result<int>> PayAsync(int subscription, decimal amount = 350m, int periods = 1, string? operation = null,
            PaymentMethod method = PaymentMethod.Transfer, string currency = "PEN", DateOnly? paidOn = null)
            => SendAsync(uow => new RegisterPaymentCommandHandler(uow, _clock, new TestLocalizer()).Handle(new RegisterPaymentCommand
            {
                RegisterPaymentRequestDto = new RegisterPaymentRequestDto
                {
                    SubscriptionId = subscription, Amount = amount, Periods = periods, OperationNumber = operation, Method = method,
                    Currency = currency, PaidOn = paidOn
                }
            }, CancellationToken.None));

        private Task<Result<int>> VoidAsync(int payment)
            => SendAsync(uow => new VoidPaymentCommandHandler(uow, _clock, new TestLocalizer()).Handle(new VoidPaymentCommand
            {
                VoidPaymentRequestDto = new VoidPaymentRequestDto { Id = payment, Reason = "operación rechazada por el banco" }
            }, CancellationToken.None));

        private async Task<(Subscription Subscription, Payment? Payment)> LoadAsync(int subscription, int? payment = null)
        {
            await using var db = _database.CreateContext(_user);
            var s = await db.Set<Subscription>().AsNoTracking().SingleAsync(x => x.Id == subscription);
            var p = payment is null ? null : await db.Set<Payment>().AsNoTracking().SingleAsync(x => x.Id == payment);
            return (s, p);
        }

        [Fact]
        public async Task A_payment_covers_the_next_period_without_gaps_and_activates_the_subscription()
        {
            var sub = await SubscribeAsync(Erp); // sin prueba ni pago: en gracia
            Assert.Equal(SubscriptionStatus.Grace, (await LoadAsync(sub)).Subscription.Status);

            var first = Ok(await PayAsync(sub, operation: " op-001 "));
            var (s, p) = await LoadAsync(sub, first);
            Assert.Equal((Nov1, new DateOnly(2026, 11, 30), (DateOnly?)null), (p!.PeriodStart, p.PeriodEnd, p.PreviousPaidUntil));
            Assert.Equal(("OP-001", 350m, Nov1, "cobranzas"), (p.OperationNumber, p.ExpectedAmount, p.PaidOn, p.CreatedBy));
            Assert.Equal((SubscriptionStatus.Active, new DateOnly(2026, 11, 30)), (s.Status, s.PaidUntil!.Value));

            // Dos meses más, con descuento: empieza al día siguiente del anterior.
            var second = Ok(await PayAsync(sub, amount: 600m, periods: 2));
            (s, p) = await LoadAsync(sub, second);
            Assert.Equal((new DateOnly(2026, 12, 1), new DateOnly(2027, 1, 31), 700m), (p!.PeriodStart, p.PeriodEnd, p.ExpectedAmount));
            Assert.Equal(new DateOnly(2027, 1, 31), s.PaidUntil);
        }

        [Fact]
        public async Task Paying_during_the_trial_starts_after_it_and_a_yearly_cycle_covers_twelve_months()
        {
            var trial = await SubscribeAsync(ErpTrial); // prueba hasta el 14/11
            var (_, p) = await LoadAsync(trial, Ok(await PayAsync(trial)));
            Assert.Equal((new DateOnly(2026, 11, 15), new DateOnly(2026, 12, 14)), (p!.PeriodStart, p.PeriodEnd));

            Ok(await SendAsync(uow => new CancelSubscriptionCommandHandler(uow, _clock, new TestLocalizer()).Handle(new CancelSubscriptionCommand
            {
                SubscriptionReasonRequestDto = new SubscriptionReasonRequestDto { Id = trial, Reason = "cambia a anual" }
            }, CancellationToken.None)));
            var yearly = await SubscribeAsync(ErpYearly, BillingCycle.Yearly);
            var (s, yp) = await LoadAsync(yearly, Ok(await PayAsync(yearly, amount: 3500m)));
            Assert.Equal((Nov1, new DateOnly(2027, 10, 31), 3500m), (yp!.PeriodStart, yp.PeriodEnd, yp.ExpectedAmount));
            Assert.Equal(SubscriptionStatus.Active, s.Status);
        }

        [Fact]
        public async Task After_a_long_suspension_the_new_period_starts_today()
        {
            var sub = await SubscribeAsync(Erp); // gracia 5 días desde el 01/11
            _clock.SetLimaDate(new DateOnly(2027, 1, 10));

            var (s, p) = await LoadAsync(sub, Ok(await PayAsync(sub)));
            Assert.Equal((new DateOnly(2027, 1, 10), new DateOnly(2027, 2, 9)), (p!.PeriodStart, p.PeriodEnd));
            Assert.Equal(SubscriptionStatus.Active, s.Status);
        }

        [Fact]
        public async Task Duplicates_currency_future_dates_and_cancelled_subscriptions_are_rejected()
        {
            var sub = await SubscribeAsync(Erp);
            Ok(await PayAsync(sub, operation: "123"));

            var duplicate = await PayAsync(sub, operation: "123");
            Assert.Contains(RegisterPaymentCommandHandler.DuplicateOperationMessage(PaymentMethod.Transfer, "123"), duplicate.Messages);
            Ok(await PayAsync(sub, operation: "123", method: PaymentMethod.Yape)); // otro medio: otra operación

            Assert.Contains(RegisterPaymentCommandHandler.CurrencyMessage("PEN"), (await PayAsync(sub, currency: "USD")).Messages);
            Assert.Contains(RegisterPaymentCommandHandler.FutureDateMessage, (await PayAsync(sub, paidOn: Nov1.AddDays(1))).Messages);

            Ok(await SendAsync(uow => new CancelSubscriptionCommandHandler(uow, _clock, new TestLocalizer()).Handle(new CancelSubscriptionCommand
            {
                SubscriptionReasonRequestDto = new SubscriptionReasonRequestDto { Id = sub, Reason = "baja" }
            }, CancellationToken.None)));
            Assert.Contains(RegisterPaymentCommandHandler.CancelledMessage, (await PayAsync(sub)).Messages);
        }

        [Fact]
        public async Task Only_the_last_payment_is_voided_and_it_restores_the_previous_date_and_status()
        {
            var sub = await SubscribeAsync(Erp);
            var first = Ok(await PayAsync(sub, operation: "A1"));
            var second = Ok(await PayAsync(sub, operation: "A2"));

            Assert.Contains(VoidPaymentCommandHandler.NotLastMessage, (await VoidAsync(first)).Messages);

            Ok(await VoidAsync(second));
            var (s, p) = await LoadAsync(sub, second);
            Assert.Equal((PaymentStatus.Voided, Nov1, "operación rechazada por el banco"), (p!.Status, p.VoidedOn!.Value, p.VoidReason));
            Assert.Equal(new DateOnly(2026, 11, 30), s.PaidUntil);
            Assert.Contains(VoidPaymentCommandHandler.AlreadyVoidedMessage, (await VoidAsync(second)).Messages);

            Ok(await VoidAsync(first)); // ahora es el último: vuelve a no tener pagos
            (s, _) = await LoadAsync(sub);
            Assert.Equal(((DateOnly?)null, SubscriptionStatus.Grace), (s.PaidUntil, s.Status));

            Ok(await PayAsync(sub, operation: "A2")); // la operación anulada se puede volver a registrar
        }

        [Fact]
        public async Task A_payment_followed_by_an_extension_cannot_be_voided()
        {
            var sub = await SubscribeAsync(Erp);
            var payment = Ok(await PayAsync(sub));
            Ok(await SendAsync(uow => new ExtendSubscriptionCommandHandler(uow, _clock, new TestLocalizer()).Handle(new ExtendSubscriptionCommand
            {
                ExtendSubscriptionRequestDto = new ExtendSubscriptionRequestDto { Id = sub, PaidUntil = new DateOnly(2026, 12, 15), Reason = "cortesía" }
            }, CancellationToken.None)));

            Assert.Contains(VoidPaymentCommandHandler.ChangedAfterMessage, (await VoidAsync(payment)).Messages);
        }

        [Fact]
        public async Task The_list_filters_by_customer_dates_and_status_newest_first()
        {
            var sub = await SubscribeAsync(Erp);
            Ok(await PayAsync(sub, paidOn: new DateOnly(2026, 10, 20)));
            var last = Ok(await PayAsync(sub, operation: "X9"));
            Ok(await VoidAsync(last));

            await using var db = _database.CreateContext(_user);
            var handler = new GetAllPaymentsPagedQueryHandler(HandlerKit.UnitOfWork(db, _user), new TestLocalizer());

            var all = await handler.Handle(new GetAllPaymentsPagedQuery { CustomerId = Alfa }, CancellationToken.None);
            Assert.Equal([Nov1, new DateOnly(2026, 10, 20)], all.Data.Select(p => p.PaidOn));
            Assert.All(all.Data, p => Assert.Equal(("ALFA SAC", "ERP Básico", "Transferencia"), (p.CustomerName, p.PlanName, p.MethodName)));

            var october = await handler.Handle(new GetAllPaymentsPagedQuery
            {
                BaseFiltersRequest = new BaseFiltersRequest { StartDate = "2026-10-01", EndDate = "2026-10-31" }
            }, CancellationToken.None);
            Assert.Equal(1, october.TotalRecords);

            var voided = await handler.Handle(new GetAllPaymentsPagedQuery { Status = PaymentStatus.Voided }, CancellationToken.None);
            Assert.Equal(["Anulado"], voided.Data.Select(p => p.StatusName));
        }
    }
}
