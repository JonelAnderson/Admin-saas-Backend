using DS.Platform.Application.DTOs.Deployments.Request;
using DS.Platform.Application.Features.Deployments.Common;
using DS.Platform.Application.Interfaces.Repositories;
using DS.Platform.Application.Interfaces.Services;
using DS.Platform.Application.Interfaces.Services.Coolify;
using DS.Platform.Domain.Entities.Ops;
using DS.Platform.Domain.Enums;
using DS.Platform.Shared.Localization;
using DS.Platform.Shared.Wrapper;
using MediatR;
using Microsoft.Extensions.Localization;

namespace DS.Platform.Application.Features.Deployments.Commands.Upgrade
{
    /// <summary>
    /// Pide llevar el stack de una instancia a otra versión de las imágenes (fase C5). No llama a Coolify: guarda el despliegue y
    /// el despachador cambia <c>ERP_VERSION</c> y reinicia el servicio. El ERP aplica sus migraciones al arrancar (servicio
    /// <c>migrate</c> del stack).
    /// </summary>
    public class UpgradeInstanceCommand : IRequest<Result<int>>
    {
        public UpgradeInstanceRequestDto? UpgradeInstanceRequestDto { get; set; }
    }

    internal class UpgradeInstanceCommandHandler(
        IUnitOfWork<int> unitOfWork,
        IDateTimeService dateTime,
        IProvisioningSettings settings,
        IStringLocalizer<ServerCommonResources> localizer) : IRequestHandler<UpgradeInstanceCommand, Result<int>>
    {
        public const string NotProvisionedMessage = "La instancia no fue montada por la consola (o su montaje no terminó): solo se actualizan los stacks montados aquí.";
        public const string SameVersionMessage = "La instancia ya está en esa versión.";
        public const string OpenDeploymentMessage = "La instancia tiene un despliegue sin terminar: espere a que termine, reintente o cancélelo.";

        public async Task<Result<int>> Handle(UpgradeInstanceCommand command, CancellationToken cancellationToken)
        {
            var dto = command.UpgradeInstanceRequestDto!;

            if (!settings.IsConfigured)
            {
                return await Result<int>.FailAsync(ProvisioningRules.NotConfiguredMessage);
            }
            if (ProvisioningRules.ValidateVersion(dto.Version, out var version) is { } versionError)
            {
                return await Result<int>.FailAsync(versionError);
            }

            var instance = await unitOfWork.ErpInstances.GetByIdAsync(dto.InstanceId);
            if (instance is null || instance.IsDeleted)
            {
                return await Result<int>.FailAsync(localizer["MessageQueryEmpty"]);
            }
            if (string.IsNullOrEmpty(instance.CoolifyServiceUuid) || string.IsNullOrEmpty(instance.DeployedVersion))
            {
                return await Result<int>.FailAsync(NotProvisionedMessage);
            }
            if (string.Equals(instance.DeployedVersion, version, StringComparison.Ordinal))
            {
                return await Result<int>.FailAsync(SameVersionMessage);
            }
            if (await unitOfWork.InstanceDeployments.GetOpenForInstanceAsync(instance.Id, cancellationToken) is not null)
            {
                return await Result<int>.FailAsync(OpenDeploymentMessage);
            }

            var deployment = new InstanceDeployment
            {
                InstanceId = instance.Id,
                CustomerId = instance.CustomerId,
                Type = DeploymentType.Update,
                Version = version,
                Status = RemoteActionStatus.Pending,
                NextAttemptOnUtc = dateTime.NowUtc
            };
            await unitOfWork.InstanceDeployments.AddAsync(deployment);
            await unitOfWork.Commit(cancellationToken);

            return await Result<int>.SuccessAsync(deployment.Id, localizer["MessageSave"]);
        }
    }
}
