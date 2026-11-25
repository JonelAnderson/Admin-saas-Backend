using DS.Platform.Application.Features.Credentials.Common;
using DS.Platform.Application.Interfaces.Repositories;
using DS.Platform.Application.Interfaces.Services;
using DS.Platform.Domain.Enums;
using DS.Platform.Shared.Wrapper;
using MediatR;

namespace DS.Platform.Application.Features.Credentials.Commands.Retry
{
    /// <summary>Devuelve a Pendiente una entrega fallida o abandonada (el despachador la vuelve a intentar desde cero).</summary>
    public class RetryApiCredentialDeliveryCommand : IRequest<Result<int>>
    {
        public int Id { get; set; }
    }

    internal class RetryApiCredentialDeliveryCommandHandler(
        IUnitOfWork<int> unitOfWork,
        IDateTimeService dateTime) : IRequestHandler<RetryApiCredentialDeliveryCommand, Result<int>>
    {
        public const string NotFoundMessage = "La entrega de credenciales no existe.";
        public const string NotRetryableMessage = "Solo se reintentan las entregas fallidas o abandonadas. Una vencida se entrega de nuevo.";
        public const string OpenMessage = "Ese emisor ya tiene otra entrega de credenciales abierta.";
        public const string DoneMessage = "Reintento programado.";

        public async Task<Result<int>> Handle(RetryApiCredentialDeliveryCommand command, CancellationToken cancellationToken)
        {
            var delivery = await unitOfWork.ApiCredentialDeliveries.GetByIdAsync(command.Id);
            if (delivery is null || delivery.IsDeleted)
            {
                return await Result<int>.FailAsync(NotFoundMessage);
            }
            if (delivery.Status is not (ApiCredentialDeliveryStatus.Failed or ApiCredentialDeliveryStatus.Abandoned))
            {
                return await Result<int>.FailAsync(NotRetryableMessage);
            }
            // Una Abandonada ya no cuenta como abierta: alguien pudo pedir otra entrega del mismo emisor mientras tanto.
            if (!CredentialDeliveryRules.IsOpen(delivery.Status)
                && await unitOfWork.ApiCredentialDeliveries.HasOpenForIssuerAsync(delivery.IssuerId, cancellationToken))
            {
                return await Result<int>.FailAsync(OpenMessage);
            }

            delivery.Status = ApiCredentialDeliveryStatus.Pending;
            delivery.Attempts = 0;
            delivery.NextAttemptOnUtc = dateTime.NowUtc;
            delivery.LockedUntilUtc = null;
            await unitOfWork.ApiCredentialDeliveries.UpdateAsync(delivery);
            await unitOfWork.Commit(cancellationToken);
            return await Result<int>.SuccessAsync(delivery.Id, DoneMessage);
        }
    }
}
