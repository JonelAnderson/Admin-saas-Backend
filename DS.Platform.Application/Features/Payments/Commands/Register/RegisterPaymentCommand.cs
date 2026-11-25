using DS.Platform.Application.DTOs.Payments.Request;
using DS.Platform.Application.Features.Notifications.Common;
using DS.Platform.Application.Features.Subscriptions;
using DS.Platform.Application.Helpers;
using DS.Platform.Application.Features.ApiSunat.Common;
using DS.Platform.Application.Interfaces.Repositories;
using DS.Platform.Application.Interfaces.Services;
using DS.Platform.Domain.Entities.Billing;
using DS.Platform.Domain.Enums;
using DS.Platform.Shared.Localization;
using DS.Platform.Shared.Wrapper;
using MediatR;
using Microsoft.Extensions.Localization;

namespace DS.Platform.Application.Features.Payments.Commands.Register
{
    /// <summary>
    /// Registra el pago de una suscripción: calcula el periodo que cubre (<see cref="PaymentPeriodCalculator"/>), extiende
    /// "pagado hasta" y recalcula el estado en el mismo guardado (reactiva si estaba en gracia o suspendida por falta de pago;
    /// una suspensión manual se mantiene). Dos registros a la vez sobre la misma suscripción: el segundo choca por xmin (409).
    /// </summary>
    public class RegisterPaymentCommand : IRequest<Result<int>>
    {
        public RegisterPaymentRequestDto? RegisterPaymentRequestDto { get; set; }
    }

    internal class RegisterPaymentCommandHandler(
        IUnitOfWork<int> unitOfWork,
        IDateTimeService dateTime,
        IStringLocalizer<ServerCommonResources> localizer) : IRequestHandler<RegisterPaymentCommand, Result<int>>
    {
        public const string CancelledMessage = "La suscripción está cancelada: no recibe pagos.";
        public const string FutureDateMessage = "La fecha del pago no puede ser futura.";

        public static string CurrencyMessage(string currency) => $"El pago debe ser en {currency}, la moneda de la suscripción.";

        public static string DuplicateOperationMessage(PaymentMethod method, string operation)
            => $"Ya hay un pago registrado por {PaymentPeriodCalculator.MethodName(method)} con la operación {operation}.";

        public async Task<Result<int>> Handle(RegisterPaymentCommand command, CancellationToken cancellationToken)
        {
            var dto = command.RegisterPaymentRequestDto!;

            var subscription = await unitOfWork.Subscriptions.GetByIdAsync(dto.SubscriptionId);
            if (subscription is null || subscription.IsDeleted)
            {
                return await Result<int>.FailAsync(localizer["MessageQueryEmpty"]);
            }
            if (subscription.Status == SubscriptionStatus.Cancelled)
            {
                return await Result<int>.FailAsync(CancelledMessage);
            }

            var currency = dto.Currency.Trim().ToUpperInvariant();
            if (currency != subscription.Currency)
            {
                return await Result<int>.FailAsync(CurrencyMessage(subscription.Currency));
            }

            var today = LimaDate.Today(dateTime.NowUtc);
            var paidOn = dto.PaidOn ?? today;
            if (paidOn > today)
            {
                return await Result<int>.FailAsync(FutureDateMessage);
            }

            var operation = string.IsNullOrWhiteSpace(dto.OperationNumber) ? null : dto.OperationNumber.Trim().ToUpperInvariant();
            if (operation is not null && await unitOfWork.Payments.IsOperationRegisteredAsync(dto.Method, operation, cancellationToken))
            {
                return await Result<int>.FailAsync(DuplicateOperationMessage(dto.Method, operation));
            }

            var (start, end) = PaymentPeriodCalculator.For(subscription, dto.Periods, today);
            var payment = new Payment
            {
                SubscriptionId = subscription.Id,
                CustomerId = subscription.CustomerId,
                PaidOn = paidOn,
                Amount = dto.Amount,
                Currency = currency,
                ExpectedAmount = PaymentPeriodCalculator.ExpectedAmount(subscription, dto.Periods),
                Method = dto.Method,
                OperationNumber = operation,
                Periods = dto.Periods,
                PeriodStart = start,
                PeriodEnd = end,
                PreviousPaidUntil = subscription.PaidUntil,
                Notes = string.IsNullOrWhiteSpace(dto.Notes) ? null : dto.Notes.Trim()
            };

            subscription.PaidUntil = end;
            SubscriptionStatusCalculator.Apply(subscription, today);

            await unitOfWork.Payments.AddAsync(payment);
            await unitOfWork.Subscriptions.UpdateAsync(subscription);
            // Constancia por correo: se encola en este mismo guardado (el job la envía).
            await QueueReceiptAsync(subscription, payment, dateTime.NowUtc, cancellationToken);
            // El corte efectivo llega a la API SUNAT por la bandeja de acciones remotas, en este mismo guardado.
            await ApiAccessReconciler.ReconcileAsync(unitOfWork, subscription.CustomerId, dateTime.NowUtc, cancellationToken);
            await unitOfWork.Commit(cancellationToken);

            return await Result<int>.SuccessAsync(payment.Id, localizer["MessageSave"]);
        }

        private async Task QueueReceiptAsync(Subscription subscription, Payment payment, DateTime nowUtc, CancellationToken cancellationToken)
        {
            var customer = await unitOfWork.Customers.GetByIdAsync(subscription.CustomerId);
            var plan = await unitOfWork.Plans.GetByIdAsync(subscription.PlanId);
            if (customer is null || plan is null)
            {
                return;
            }
            // El Id del pago aún no existe (se asigna al guardar): la referencia sale de la suscripción y del fin del periodo.
            var reference = $"C{subscription.Id:D4}-{payment.PeriodEnd:yyyyMMdd}";
            var (subject, body) = NoticeTemplates.PaymentReceiptEmail(new PaymentReceipt(
                reference, customer.BusinessName, customer.Ruc, NotificationOutbox.ProductName(subscription.Product), plan.Name, payment.PaidOn,
                payment.Amount, payment.Currency, PaymentPeriodCalculator.MethodName(payment.Method), payment.OperationNumber, payment.Periods,
                payment.PeriodStart, payment.PeriodEnd, subscription.CoveredUntil));
            var key = $"payment:{subscription.Id}:{payment.PeriodEnd:yyyyMMdd}:{payment.Method}:{payment.OperationNumber ?? payment.PaidOn.ToString("yyyyMMdd")}:{nowUtc.Ticks}";
            await NotificationOutbox.EnqueueAsync(unitOfWork,
                [new NoticeSpec(NotificationType.PaymentRegistered, customer, key, subject, body, SubscriptionId: subscription.Id, Payment: payment)],
                nowUtc, cancellationToken);
        }
    }
}
