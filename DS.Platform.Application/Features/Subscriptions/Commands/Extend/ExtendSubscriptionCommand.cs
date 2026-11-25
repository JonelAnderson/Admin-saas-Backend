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

namespace DS.Platform.Application.Features.Subscriptions.Commands.Extend
{
    /// <summary>
    /// Prórroga manual: mueve "pagado hasta" a una fecha posterior, con motivo (queda en la suscripción y en la auditoría).
    /// Reactiva si estaba en gracia o suspendida por falta de pago; no levanta una suspensión manual.
    /// </summary>
    public class ExtendSubscriptionCommand : IRequest<Result<int>>
    {
        public ExtendSubscriptionRequestDto? ExtendSubscriptionRequestDto { get; set; }
    }

    internal class ExtendSubscriptionCommandHandler(
        IUnitOfWork<int> unitOfWork,
        IDateTimeService dateTime,
        IStringLocalizer<ServerCommonResources> localizer) : IRequestHandler<ExtendSubscriptionCommand, Result<int>>
    {
        public const string NotLaterMessage = "La nueva fecha debe ser posterior a la fecha pagada actual y no anterior a hoy.";

        public async Task<Result<int>> Handle(ExtendSubscriptionCommand command, CancellationToken cancellationToken)
        {
            var dto = command.ExtendSubscriptionRequestDto!;

            var (subscription, error) = await CreateSubscriptionCommandHandler.LoadOpenAsync(unitOfWork, dto.Id, localizer);
            if (subscription is null)
            {
                return await Result<int>.FailAsync(error!);
            }

            var today = LimaDate.Today(dateTime.NowUtc);
            if (dto.PaidUntil < today || (subscription.PaidUntil is { } current && dto.PaidUntil <= current))
            {
                return await Result<int>.FailAsync(NotLaterMessage);
            }

            subscription.PaidUntil = dto.PaidUntil;
            subscription.StatusReason = $"Prórroga hasta {dto.PaidUntil:dd/MM/yyyy}: {dto.Reason.Trim()}";
            SubscriptionStatusCalculator.Apply(subscription, today);

            await unitOfWork.Subscriptions.UpdateAsync(subscription);
            // El corte efectivo llega a la API SUNAT por la bandeja de acciones remotas, en este mismo guardado.
            await ApiAccessReconciler.ReconcileAsync(unitOfWork, subscription.CustomerId, dateTime.NowUtc, cancellationToken);
            await unitOfWork.Commit(cancellationToken);

            return await Result<int>.SuccessAsync(subscription.Id, localizer["MessageUpdate"]);
        }
    }
}
