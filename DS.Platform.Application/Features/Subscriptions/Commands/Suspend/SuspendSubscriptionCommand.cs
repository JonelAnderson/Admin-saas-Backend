using DS.Platform.Application.DTOs.Subscriptions.Request;
using DS.Platform.Application.Features.Subscriptions.Commands.Create;
using DS.Platform.Application.Helpers;
using DS.Platform.Application.Features.ApiSunat.Common;
using DS.Platform.Application.Interfaces.Repositories;
using DS.Platform.Application.Interfaces.Services;
using DS.Platform.Shared.Localization;
using DS.Platform.Shared.Wrapper;
using MediatR;
using Microsoft.Extensions.Localization;

namespace DS.Platform.Application.Features.Subscriptions.Commands.Suspend
{
    /// <summary>
    /// Suspensión manual con motivo, aunque esté al día. El corte efectivo lo aplican el ERP (licencia, fase C4) y la API SUNAT
    /// (acción remota, fase C3) cuando existan; hoy queda el estado en la consola.
    /// </summary>
    public class SuspendSubscriptionCommand : IRequest<Result<int>>
    {
        public SubscriptionReasonRequestDto? SubscriptionReasonRequestDto { get; set; }
    }

    internal class SuspendSubscriptionCommandHandler(
        IUnitOfWork<int> unitOfWork,
        IDateTimeService dateTime,
        IStringLocalizer<ServerCommonResources> localizer) : IRequestHandler<SuspendSubscriptionCommand, Result<int>>
    {
        public const string AlreadySuspendedMessage = "La suscripción ya está suspendida a mano.";

        public async Task<Result<int>> Handle(SuspendSubscriptionCommand command, CancellationToken cancellationToken)
        {
            var dto = command.SubscriptionReasonRequestDto!;

            var (subscription, error) = await CreateSubscriptionCommandHandler.LoadOpenAsync(unitOfWork, dto.Id, localizer);
            if (subscription is null)
            {
                return await Result<int>.FailAsync(error!);
            }
            if (subscription.SuspendedManually)
            {
                return await Result<int>.FailAsync(AlreadySuspendedMessage);
            }

            subscription.SuspendedManually = true;
            subscription.StatusReason = $"Suspendida: {dto.Reason.Trim()}";
            SubscriptionStatusCalculator.Apply(subscription, LimaDate.Today(dateTime.NowUtc));

            await unitOfWork.Subscriptions.UpdateAsync(subscription);
            // El corte efectivo llega a la API SUNAT por la bandeja de acciones remotas, en este mismo guardado.
            await ApiAccessReconciler.ReconcileAsync(unitOfWork, subscription.CustomerId, dateTime.NowUtc, cancellationToken);
            await unitOfWork.Commit(cancellationToken);

            return await Result<int>.SuccessAsync(subscription.Id, localizer["MessageUpdate"]);
        }
    }
}
