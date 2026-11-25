using DS.Platform.Application.DTOs.Deployments.Request;
using DS.Platform.Application.DTOs.Deployments.Response;
using DS.Platform.Application.Features.Deployments.Common;
using DS.Platform.Application.Features.Instances.Common;
using DS.Platform.Application.Helpers;
using DS.Platform.Application.Interfaces.Repositories;
using DS.Platform.Application.Interfaces.Services;
using DS.Platform.Application.Interfaces.Services.Coolify;
using DS.Platform.Application.Interfaces.Services.Licensing;
using DS.Platform.Application.Interfaces.Services.Security;
using DS.Platform.Domain.Entities.Ops;
using DS.Platform.Domain.Enums;
using DS.Platform.Shared.Localization;
using DS.Platform.Shared.Wrapper;
using MediatR;
using Microsoft.Extensions.Localization;

namespace DS.Platform.Application.Features.Deployments.Commands.Provision
{
    /// <summary>
    /// Pide montar en Coolify el stack de una instancia registrada (fase C5). No llama a Coolify: genera los secretos del stack, los
    /// guarda cifrados con el despliegue y deja la instancia lista para que el despachador (<c>DeploymentsDispatch</c>) haga el
    /// trabajo con reintentos. La clave de licencia se renueva y va directo al stack; la contraseña inicial de los usuarios del ERP
    /// se devuelve **una sola vez**.
    /// </summary>
    public class ProvisionInstanceCommand : IRequest<Result<ProvisionInstanceResponseDto>>
    {
        public ProvisionInstanceRequestDto? ProvisionInstanceRequestDto { get; set; }
    }

    internal class ProvisionInstanceCommandHandler(
        IUnitOfWork<int> unitOfWork,
        IDateTimeService dateTime,
        IProvisioningSettings settings,
        IProvisioningSecretsGenerator secretsGenerator,
        ILicenseSigner signer,
        ISecretProtector protector,
        IStringLocalizer<ServerCommonResources> localizer) : IRequestHandler<ProvisionInstanceCommand, Result<ProvisionInstanceResponseDto>>
    {
        public const string InstanceNotFoundMessage = "La instancia no existe.";
        public const string InstanceInactiveMessage = "La instancia o su cliente están inactivos: actívelos antes de montar el stack.";
        public const string AlreadyProvisionedMessage = "La instancia ya tiene su stack en Coolify.";
        public const string AlreadyConnectedMessage = "La instancia ya se conectó: ya está montada a mano. La consola solo monta instancias que aún no se conectan.";
        public const string OpenDeploymentMessage = "La instancia tiene un despliegue sin terminar: espere a que termine, reintente o cancélelo.";
        public const string NoSubscriptionMessage = "El cliente no tiene una suscripción vigente del ERP: contrate un plan antes de montar la instancia (nacería en solo lectura).";
        public const string NoSigningKeyMessage = "La consola no tiene la llave de firma de licencias (License:SigningKey): sin ella el ERP no podría validar su licencia.";

        public async Task<Result<ProvisionInstanceResponseDto>> Handle(ProvisionInstanceCommand command, CancellationToken cancellationToken)
        {
            var dto = command.ProvisionInstanceRequestDto!;

            if (!settings.IsConfigured)
            {
                return await Result<ProvisionInstanceResponseDto>.FailAsync(ProvisioningRules.NotConfiguredMessage);
            }
            if (!signer.IsConfigured)
            {
                return await Result<ProvisionInstanceResponseDto>.FailAsync(NoSigningKeyMessage);
            }
            if (ProvisioningRules.ValidateSubdomain(dto.Subdomain, out var subdomain) is { } subdomainError)
            {
                return await Result<ProvisionInstanceResponseDto>.FailAsync(subdomainError);
            }
            if (ProvisioningRules.ValidateVersion(dto.Version, out var version) is { } versionError)
            {
                return await Result<ProvisionInstanceResponseDto>.FailAsync(versionError);
            }

            var instance = await unitOfWork.ErpInstances.GetByIdAsync(dto.InstanceId);
            if (instance is null || instance.IsDeleted)
            {
                return await Result<ProvisionInstanceResponseDto>.FailAsync(InstanceNotFoundMessage);
            }
            var customer = await unitOfWork.Customers.GetByIdAsync(instance.CustomerId);
            if (!instance.IsActive || customer is null || customer.IsDeleted || !customer.IsActive)
            {
                return await Result<ProvisionInstanceResponseDto>.FailAsync(InstanceInactiveMessage);
            }
            if (!string.IsNullOrEmpty(instance.CoolifyServiceUuid))
            {
                return await Result<ProvisionInstanceResponseDto>.FailAsync(AlreadyProvisionedMessage);
            }
            if (instance.LastContactOnUtc is not null)
            {
                return await Result<ProvisionInstanceResponseDto>.FailAsync(AlreadyConnectedMessage);
            }
            if (await unitOfWork.InstanceDeployments.GetOpenForInstanceAsync(instance.Id, cancellationToken) is not null)
            {
                return await Result<ProvisionInstanceResponseDto>.FailAsync(OpenDeploymentMessage);
            }

            var now = dateTime.NowUtc;
            var subscriptions = await unitOfWork.Subscriptions.GetByCustomerAsync(instance.CustomerId, cancellationToken);
            var license = LicenseStateBuilder.Build(subscriptions, LimaDate.Today(now));
            if (license.Status is LicenseStatus.Suspended or LicenseStatus.Cancelled)
            {
                return await Result<ProvisionInstanceResponseDto>.FailAsync(NoSubscriptionMessage);
            }

            var host = ProvisioningRules.HostFor(subdomain, settings.BaseDomain!);
            var url = ProvisioningRules.UrlFor(host);
            if (await unitOfWork.ErpInstances.UrlInUseAsync(url, instance.Id, cancellationToken))
            {
                return await Result<ProvisionInstanceResponseDto>.FailAsync(ProvisioningRules.HostTakenMessage);
            }

            // La clave de licencia nace aquí y va directo al stack: en la base solo queda su hash y, cifrada y hasta que el stack
            // arranca, junto al despliegue. Como la instancia nunca se conectó, renovarla no deja a nadie sin servicio.
            var licenseKey = LicenseKeys.Generate();
            var secrets = secretsGenerator.Generate(licenseKey, signer.PublicKey);
            instance.LicenseKeyHash = LicenseKeys.Hash(licenseKey);
            instance.LicenseKeyHint = LicenseKeys.Hint(licenseKey);
            instance.KeyRotatedOnUtc = now;
            instance.Url = url;
            instance.Server = "Coolify";
            await unitOfWork.ErpInstances.UpdateAsync(instance);

            var deployment = new InstanceDeployment
            {
                InstanceId = instance.Id,
                CustomerId = instance.CustomerId,
                Type = DeploymentType.Create,
                Version = version,
                Step = DeploymentStep.None,
                SecretsProtected = protector.Protect(ProvisioningSecretsJson.Serialize(secrets)),
                Status = RemoteActionStatus.Pending,
                NextAttemptOnUtc = now
            };
            await unitOfWork.InstanceDeployments.AddAsync(deployment);
            await unitOfWork.Commit(cancellationToken);

            return await Result<ProvisionInstanceResponseDto>.SuccessAsync(new ProvisionInstanceResponseDto
            {
                DeploymentId = deployment.Id,
                Url = url,
                InitialAdminUser = "administrador",
                InitialAdminPassword = secrets.InitialAdminPassword
            }, localizer["MessageSave"]);
        }
    }
}
