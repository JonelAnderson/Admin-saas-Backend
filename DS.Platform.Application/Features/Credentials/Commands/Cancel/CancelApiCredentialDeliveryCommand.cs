using DS.Platform.Application.Features.Credentials.Common;
using DS.Platform.Application.Interfaces.Repositories;
using DS.Platform.Application.Interfaces.Services;
using DS.Platform.Domain.Enums;
using DS.Platform.Shared.Wrapper;
using MediatR;

namespace DS.Platform.Application.Features.Credentials.Commands.Cancel
{
    /// <summary>
    /// Cancela una entrega de credenciales que no terminó (Pendiente, Fallida, Abandonada o Lista) y borra su secreto. Una entrega
    /// En curso no se toca (la está ejecutando el despachador). Si ya estaba Lista, la API ya había generado esas credenciales y
    /// las anteriores dejaron de funcionar: el mensaje lo dice y habrá que entregar de nuevo.
    /// </summary>
    public class CancelApiCredentialDeliveryCommand : IRequest<Result<int>>
    {
        public int Id { get; set; }
    }

    internal class CancelApiCredentialDeliveryCommandHandler(
        IUnitOfWork<int> unitOfWork,
        IDateTimeService dateTime) : IRequestHandler<CancelApiCredentialDeliveryCommand, Result<int>>
    {
        public const string NotFoundMessage = "La entrega de credenciales no existe.";
        public const string BusyMessage = "La entrega se está ejecutando en este momento: inténtelo en un minuto.";
        public const string NotCancellableMessage = "Solo se cancelan las entregas pendientes, fallidas, abandonadas o listas.";
        public const string DoneMessage = "Entrega cancelada.";
        public const string DoneReadyMessage =
            "Entrega cancelada y credenciales descartadas. La API ya las había generado: las credenciales anteriores del emisor dejaron de funcionar, entregue de nuevo.";

        public async Task<Result<int>> Handle(CancelApiCredentialDeliveryCommand command, CancellationToken cancellationToken)
        {
            var delivery = await unitOfWork.ApiCredentialDeliveries.GetByIdAsync(command.Id);
            if (delivery is null || delivery.IsDeleted)
            {
                return await Result<int>.FailAsync(NotFoundMessage);
            }
            if (delivery.Status == ApiCredentialDeliveryStatus.Sending && delivery.LockedUntilUtc > dateTime.NowUtc)
            {
                return await Result<int>.FailAsync(BusyMessage);
            }
            if (delivery.Status is not (ApiCredentialDeliveryStatus.Pending or ApiCredentialDeliveryStatus.Failed
                or ApiCredentialDeliveryStatus.Abandoned or ApiCredentialDeliveryStatus.Ready or ApiCredentialDeliveryStatus.Sending))
            {
                return await Result<int>.FailAsync(NotCancellableMessage);
            }

            var wasReady = delivery.Status == ApiCredentialDeliveryStatus.Ready;
            CredentialSecrets.Wipe(delivery);
            delivery.Status = ApiCredentialDeliveryStatus.Cancelled;
            delivery.LockedUntilUtc = null;
            await unitOfWork.ApiCredentialDeliveries.UpdateAsync(delivery);
            await unitOfWork.Commit(cancellationToken);
            return await Result<int>.SuccessAsync(delivery.Id, wasReady ? DoneReadyMessage : DoneMessage);
        }
    }
}
