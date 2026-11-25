using DS.Platform.Application.Interfaces.Repositories;
using DS.Platform.Application.Interfaces.Services;
using DS.Platform.Domain.Enums;
using DS.Platform.Shared.Localization;
using DS.Platform.Shared.Wrapper;
using MediatR;
using Microsoft.Extensions.Localization;

namespace DS.Platform.Application.Features.Deployments.Commands.Retry
{
    /// <summary>
    /// Reintenta a mano un despliegue Fallido o Abandonado: vuelve a Pendiente con los intentos en cero (un montaje continúa en el paso
    /// donde quedó). No llama a Coolify: lo hace el despachador.
    /// </summary>
    public class RetryDeploymentCommand : IRequest<Result<int>>
    {
        public int Id { get; set; }
    }

    internal class RetryDeploymentCommandHandler(
        IUnitOfWork<int> unitOfWork,
        IDateTimeService dateTime,
        IStringLocalizer<ServerCommonResources> localizer) : IRequestHandler<RetryDeploymentCommand, Result<int>>
    {
        public const string NotRetryableMessage = "Solo se reintentan los despliegues fallidos o abandonados.";

        public async Task<Result<int>> Handle(RetryDeploymentCommand request, CancellationToken cancellationToken)
        {
            var deployment = await unitOfWork.InstanceDeployments.GetByIdAsync(request.Id);
            if (deployment is null || deployment.IsDeleted)
            {
                return await Result<int>.FailAsync(localizer["MessageQueryEmpty"]);
            }
            if (deployment.Status is not (RemoteActionStatus.Failed or RemoteActionStatus.Abandoned))
            {
                return await Result<int>.FailAsync(NotRetryableMessage);
            }

            deployment.Status = RemoteActionStatus.Pending;
            deployment.Attempts = 0;
            deployment.NextAttemptOnUtc = dateTime.NowUtc;
            deployment.LockedUntilUtc = null;
            await unitOfWork.InstanceDeployments.UpdateAsync(deployment);
            await unitOfWork.Commit(cancellationToken);

            return await Result<int>.SuccessAsync(deployment.Id, localizer["MessageUpdate"]);
        }
    }
}
