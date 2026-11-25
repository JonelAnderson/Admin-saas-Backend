using DS.Platform.Application.Interfaces.Repositories;
using DS.Platform.Domain.Enums;
using DS.Platform.Shared.Wrapper;
using MediatR;

namespace DS.Platform.Application.Features.Deployments.Commands.Cancel
{
    /// <summary>
    /// Cancela un despliegue que aún no terminó (Pendiente, Fallido o Abandonado; uno En curso no, porque ya está hablando con
    /// Coolify). Borra los secretos guardados. Si un montaje ya había creado el servicio en Coolify, la instancia queda como "sin
    /// montar" y el servicio hay que eliminarlo allí a mano: la consola nunca borra stacks ni bases de clientes.
    /// </summary>
    public class CancelDeploymentCommand : IRequest<Result<int>>
    {
        public int Id { get; set; }
    }

    internal class CancelDeploymentCommandHandler(IUnitOfWork<int> unitOfWork) : IRequestHandler<CancelDeploymentCommand, Result<int>>
    {
        public const string NotFoundMessage = "El despliegue no existe.";
        public const string NotCancellableMessage = "Solo se cancelan los despliegues pendientes, fallidos o abandonados.";
        public const string DoneMessage = "Despliegue cancelado.";

        public async Task<Result<int>> Handle(CancelDeploymentCommand request, CancellationToken cancellationToken)
        {
            var deployment = await unitOfWork.InstanceDeployments.GetByIdAsync(request.Id);
            if (deployment is null || deployment.IsDeleted)
            {
                return await Result<int>.FailAsync(NotFoundMessage);
            }
            if (deployment.Status is not (RemoteActionStatus.Pending or RemoteActionStatus.Failed or RemoteActionStatus.Abandoned))
            {
                return await Result<int>.FailAsync(NotCancellableMessage);
            }

            var message = DoneMessage;
            if (deployment.Type == DeploymentType.Create && deployment.Step >= DeploymentStep.ServiceCreated)
            {
                var instance = await unitOfWork.ErpInstances.GetByIdAsync(deployment.InstanceId);
                if (instance is not null && !string.IsNullOrEmpty(instance.CoolifyServiceUuid))
                {
                    message = $"{DoneMessage} El servicio {instance.CoolifyServiceUuid} ya existe en Coolify: elimínelo allí (la consola no borra stacks).";
                    instance.CoolifyServiceUuid = null;
                    await unitOfWork.ErpInstances.UpdateAsync(instance);
                }
            }

            deployment.Status = RemoteActionStatus.Cancelled;
            deployment.LockedUntilUtc = null;
            deployment.SecretsProtected = null;
            await unitOfWork.InstanceDeployments.UpdateAsync(deployment);
            await unitOfWork.Commit(cancellationToken);

            return await Result<int>.SuccessAsync(deployment.Id, message);
        }
    }
}
