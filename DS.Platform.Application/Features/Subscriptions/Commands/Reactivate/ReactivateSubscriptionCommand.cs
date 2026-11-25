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

namespace DS.Platform.Application.Features.Subscriptions.Commands.Reactivate
{
    /// <summary>
    /// Levanta una suspensión manual. Si además está vencida, sigue suspendida por falta de pago hasta registrar un pago o una
    /// prórroga (el mensaje lo dice).
    /// </summary>
    public class ReactivateSubscriptionCommand : IRequest<Result<int>>
    {
        public SubscriptionReasonRequestDto? SubscriptionReasonRequestDto { get; set; }
    }

    internal class ReactivateSubscriptionCommandHandler(
        IUnitOfWork<int> unitOfWork,
        IDateTimeService dateTime,
        IStringLocalizer<ServerCommonResources> localizer) : IRequestHandler<ReactivateSubscriptionCommand, Result<int>>
    {
        public const string NotSuspendedMessage =
            "La suscripción no está suspendida a mano. Si está suspendida por falta de pago, registre un pago o una prórroga.";
        public const string StillUnpaidMessage =
            "Se levantó la suspensión manual, pero la suscripción sigue suspendida por falta de pago: registre un pago o una prórroga.";

        public async Task<Result<int>> Handle(ReactivateSubscriptionCommand command, CancellationToken cancellationToken)
        {
            var dto = command.SubscriptionReasonRequestDto!;

            var (subscription, error) = await CreateSubscriptionCommandHandler.LoadOpenAsync(unitOfWork, dto.Id, localizer);
            if (subscription is null)
            {
                return await Result<int>.FailAsync(error!);
            }
            if (!subscription.SuspendedManually)
            {
                return await Result<int>.FailAsync(NotSuspendedMessage);
            }

            subscription.SuspendedManually = false;
            subscription.StatusReason = $"Reactivada: {dto.Reason.Trim()}";
            SubscriptionStatusCalculator.Apply(subscription, LimaDate.Today(dateTime.NowUtc));

            await unitOfWork.Subscriptions.UpdateAsync(subscription);
            // El corte efectivo llega a la API SUNAT por la bandeja de acciones remotas, en este mismo guardado.
            await ApiAccessReconciler.ReconcileAsync(unitOfWork, subscription.CustomerId, dateTime.NowUtc, cancellationToken);
            await unitOfWork.Commit(cancellationToken);

            return await Result<int>.SuccessAsync(subscription.Id,
                subscription.Status == SubscriptionStatus.Suspended ? StillUnpaidMessage : localizer["MessageUpdate"]);
        }
    }
}
