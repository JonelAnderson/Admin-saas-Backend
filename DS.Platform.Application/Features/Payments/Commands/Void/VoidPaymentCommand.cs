using DS.Platform.Application.DTOs.Payments.Request;
using DS.Platform.Application.Features.Subscriptions;
using DS.Platform.Application.Helpers;
using DS.Platform.Application.Features.ApiSunat.Common;
using DS.Platform.Application.Interfaces.Repositories;
using DS.Platform.Application.Interfaces.Services;
using DS.Platform.Domain.Enums;
using DS.Platform.Shared.Localization;
using DS.Platform.Shared.Wrapper;
using MediatR;
using Microsoft.Extensions.Localization;

namespace DS.Platform.Application.Features.Payments.Commands.Void
{
    /// <summary>
    /// Anula un pago con motivo (nunca se edita ni se borra). Solo el último pago registrado de la suscripción y solo si nada
    /// movió "pagado hasta" después (una prórroga): así se restaura exactamente la fecha anterior y se recalcula el estado.
    /// </summary>
    public class VoidPaymentCommand : IRequest<Result<int>>
    {
        public VoidPaymentRequestDto? VoidPaymentRequestDto { get; set; }
    }

    internal class VoidPaymentCommandHandler(
        IUnitOfWork<int> unitOfWork,
        IDateTimeService dateTime,
        IStringLocalizer<ServerCommonResources> localizer) : IRequestHandler<VoidPaymentCommand, Result<int>>
    {
        public const string AlreadyVoidedMessage = "El pago ya está anulado.";
        public const string NotLastMessage = "Solo se puede anular el último pago de la suscripción: anule antes los posteriores.";
        public const string ChangedAfterMessage =
            "La fecha pagada de la suscripción cambió después de este pago (prórroga): ajústela con una prórroga antes de anular.";

        public async Task<Result<int>> Handle(VoidPaymentCommand command, CancellationToken cancellationToken)
        {
            var dto = command.VoidPaymentRequestDto!;

            var payment = await unitOfWork.Payments.GetByIdAsync(dto.Id);
            if (payment is null || payment.IsDeleted)
            {
                return await Result<int>.FailAsync(localizer["MessageQueryEmpty"]);
            }
            if (payment.Status == PaymentStatus.Voided)
            {
                return await Result<int>.FailAsync(AlreadyVoidedMessage);
            }
            if (await unitOfWork.Payments.LastRegisteredIdAsync(payment.SubscriptionId, cancellationToken) != payment.Id)
            {
                return await Result<int>.FailAsync(NotLastMessage);
            }

            var subscription = await unitOfWork.Subscriptions.GetByIdAsync(payment.SubscriptionId);
            if (subscription is null)
            {
                return await Result<int>.FailAsync(localizer["MessageQueryEmpty"]);
            }
            if (subscription.PaidUntil != payment.PeriodEnd)
            {
                return await Result<int>.FailAsync(ChangedAfterMessage);
            }

            var today = LimaDate.Today(dateTime.NowUtc);
            payment.Status = PaymentStatus.Voided;
            payment.VoidedOn = today;
            payment.VoidReason = dto.Reason.Trim();

            subscription.PaidUntil = payment.PreviousPaidUntil;
            SubscriptionStatusCalculator.Apply(subscription, today);

            await unitOfWork.Payments.UpdateAsync(payment);
            await unitOfWork.Subscriptions.UpdateAsync(subscription);
            // El corte efectivo llega a la API SUNAT por la bandeja de acciones remotas, en este mismo guardado.
            await ApiAccessReconciler.ReconcileAsync(unitOfWork, subscription.CustomerId, dateTime.NowUtc, cancellationToken);
            await unitOfWork.Commit(cancellationToken);

            return await Result<int>.SuccessAsync(payment.Id, localizer["MessageUpdate"]);
        }
    }
}
