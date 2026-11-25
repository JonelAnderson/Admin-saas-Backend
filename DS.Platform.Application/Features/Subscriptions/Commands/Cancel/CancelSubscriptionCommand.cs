using DS.Platform.Application.DTOs.Subscriptions.Request;
using DS.Platform.Application.Features.Subscriptions.Commands.Create;
using DS.Platform.Application.Helpers;
using DS.Platform.Application.Features.ApiSunat.Common;
using DS.Platform.Application.Interfaces.Repositories;
using DS.Platform.Application.Interfaces.Services;
using DS.Platform.Domain.Enums;
using DS.Platform.Shared.Localization;
using DS.Platform.Shared.Wrapper;
using MediatR;
using Microsoft.Extensions.Localization;

namespace DS.Platform.Application.Features.Subscriptions.Commands.Cancel
{
    /// <summary>
    /// Baja definitiva con motivo. Libera el producto para una suscripción nueva del cliente (sin prueba gratuita) y deja de
    /// contar como abierta (el cliente o el plan ya se pueden eliminar).
    /// </summary>
    public class CancelSubscriptionCommand : IRequest<Result<int>>
    {
        public SubscriptionReasonRequestDto? SubscriptionReasonRequestDto { get; set; }
    }

    internal class CancelSubscriptionCommandHandler(
        IUnitOfWork<int> unitOfWork,
        IDateTimeService dateTime,
        IStringLocalizer<ServerCommonResources> localizer) : IRequestHandler<CancelSubscriptionCommand, Result<int>>
    {
        public async Task<Result<int>> Handle(CancelSubscriptionCommand command, CancellationToken cancellationToken)
        {
            var dto = command.SubscriptionReasonRequestDto!;

            var (subscription, error) = await CreateSubscriptionCommandHandler.LoadOpenAsync(unitOfWork, dto.Id, localizer);
            if (subscription is null)
            {
                return await Result<int>.FailAsync(error!);
            }

            var today = LimaDate.Today(dateTime.NowUtc);
            subscription.Status = SubscriptionStatus.Cancelled;
            subscription.CancelledOn = today;
            subscription.SuspendedManually = false;
            subscription.StatusReason = $"Cancelada: {dto.Reason.Trim()}";
            SubscriptionStatusCalculator.Apply(subscription, today);

            await unitOfWork.Subscriptions.UpdateAsync(subscription);
            // El corte efectivo llega a la API SUNAT por la bandeja de acciones remotas, en este mismo guardado.
            await ApiAccessReconciler.ReconcileAsync(unitOfWork, subscription.CustomerId, dateTime.NowUtc, cancellationToken);
            await unitOfWork.Commit(cancellationToken);

            return await Result<int>.SuccessAsync(subscription.Id, localizer["MessageUpdate"]);
        }
    }
}
