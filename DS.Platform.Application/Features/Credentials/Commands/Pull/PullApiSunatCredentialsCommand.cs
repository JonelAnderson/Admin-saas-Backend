using DS.Platform.Application.DTOs.Credentials.Request;
using DS.Platform.Application.DTOs.Credentials.Response;
using DS.Platform.Application.Features.Credentials.Common;
using DS.Platform.Application.Features.Instances.Common;
using DS.Platform.Application.Features.Licensing.Commands.Check;
using DS.Platform.Application.Interfaces.Repositories;
using DS.Platform.Application.Interfaces.Services;
using DS.Platform.Application.Interfaces.Services.ApiSunat;
using DS.Platform.Application.Interfaces.Services.Security;
using DS.Platform.Domain.Enums;
using DS.Platform.Shared.Wrapper;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;

namespace DS.Platform.Application.Features.Credentials.Commands.Pull
{
    /// <summary>
    /// El ERP recoge las credenciales de la API SUNAT que la consola le tiene listas (fase C6b, <c>POST api/v1/License/ApiSunat/Pull</c>).
    /// Igual que la consulta de licencia: anónimo, la instancia se identifica con su clave (<c>X-License-Key</c>), una clave desconocida,
    /// vacía o inactiva recibe siempre el mismo mensaje y el controlador limita las consultas por IP. Solo ve **las entregas de su
    /// instancia** (por su clave, nunca por un id del cuerpo), máximo 20, y solo mientras no se hayan confirmado ni vencido. Sin la
    /// dirección pública de la API (<c>ApiSunat:PublicUrl</c>) responde una lista vacía. Solo anota cuándo las vio (para el "sin
    /// recoger" de la pantalla): las credenciales siguen en la consola hasta que el ERP las confirme con <c>Ack</c>.
    /// </summary>
    public class PullApiSunatCredentialsCommand : IRequest<Result<ApiSunatPullResponseDto>>
    {
        /// <summary>Clave de la cabecera <c>X-License-Key</c>.</summary>
        public string? LicenseKey { get; set; }
        public ApiSunatPullRequestDto? ApiSunatPullRequestDto { get; set; }
    }

    internal class PullApiSunatCredentialsCommandHandler(
        IUnitOfWork<int> unitOfWork,
        IDateTimeService dateTime,
        IApiSunatSettings settings,
        ISecretProtector protector) : IRequestHandler<PullApiSunatCredentialsCommand, Result<ApiSunatPullResponseDto>>
    {
        public const string RetryMessage = "Las entregas cambiaron mientras se leían: vuelva a consultar.";

        public async Task<Result<ApiSunatPullResponseDto>> Handle(PullApiSunatCredentialsCommand command, CancellationToken cancellationToken)
        {
            var key = command.LicenseKey?.Trim();
            if (string.IsNullOrEmpty(key) || key.Length > LicenseKeys.MaxLength)
            {
                return await Result<ApiSunatPullResponseDto>.FailAsync(CheckLicenseCommandHandler.InvalidKeyMessage);
            }
            var instance = await unitOfWork.ErpInstances.GetByKeyHashAsync(LicenseKeys.Hash(key), cancellationToken);
            if (instance is null || !instance.IsActive)
            {
                return await Result<ApiSunatPullResponseDto>.FailAsync(CheckLicenseCommandHandler.InvalidKeyMessage);
            }
            if (!settings.IsPublicUrlConfigured)
            {
                return await Result<ApiSunatPullResponseDto>.SuccessAsync(new ApiSunatPullResponseDto());
            }

            var now = dateTime.NowUtc;
            var ready = await unitOfWork.ApiCredentialDeliveries.GetReadyByInstanceAsync(
                instance.Id, now, CredentialDeliveryRules.MaxPerPull, cancellationToken);

            var response = new ApiSunatPullResponseDto { BaseUrl = settings.PublicUrl };
            foreach (var delivery in ready)
            {
                if (delivery.Status != ApiCredentialDeliveryStatus.Ready
                    || string.IsNullOrEmpty(delivery.ApiKeyProtected) || string.IsNullOrEmpty(delivery.ApiSecretProtected))
                {
                    continue;
                }
                try
                {
                    response.Deliveries.Add(new ApiSunatDeliveryDto
                    {
                        Id = delivery.Id,
                        Ruc = delivery.Ruc,
                        ApiKey = protector.Unprotect(delivery.ApiKeyProtected),
                        ApiSecret = protector.Unprotect(delivery.ApiSecretProtected)
                    });
                }
                catch (CryptographicException)
                {
                    // El secreto no se puede descifrar (cambió la llave de Data Protection): no se entrega; se verá "sin recoger" y vencerá.
                    continue;
                }
                delivery.LastPulledOnUtc = now;
                await unitOfWork.ApiCredentialDeliveries.UpdateAsync(delivery);
            }

            if (response.Deliveries.Count > 0)
            {
                try
                {
                    await unitOfWork.Commit(cancellationToken);
                }
                catch (DbUpdateConcurrencyException)
                {
                    // Mientras tanto la confirmaron, la cancelaron o venció: no se entrega lo que pudo cambiar.
                    unitOfWork.ClearChanges();
                    return await Result<ApiSunatPullResponseDto>.FailAsync(RetryMessage);
                }
            }
            return await Result<ApiSunatPullResponseDto>.SuccessAsync(response);
        }
    }
}
