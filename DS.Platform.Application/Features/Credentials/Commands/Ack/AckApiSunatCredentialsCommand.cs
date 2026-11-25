using DS.Platform.Application.DTOs.Credentials.Request;
using DS.Platform.Application.DTOs.Credentials.Response;
using DS.Platform.Application.Features.Credentials.Common;
using DS.Platform.Application.Features.Instances.Common;
using DS.Platform.Application.Features.Licensing.Commands.Check;
using DS.Platform.Application.Interfaces.Repositories;
using DS.Platform.Application.Interfaces.Services;
using DS.Platform.Domain.Enums;
using DS.Platform.Shared.Wrapper;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace DS.Platform.Application.Features.Credentials.Commands.Ack
{
    /// <summary>
    /// El ERP confirma las entregas que ya guardó (fase C6b, <c>POST api/v1/License/ApiSunat/Ack</c>): pasan a Entregada y **se borra
    /// el secreto** de la consola. Solo toca las de su instancia (la que identifica su clave): un id de otra instancia se ignora sin
    /// avisar. Idempotente: confirmar una ya entregada cuenta como confirmada; una cancelada o vencida no. Devuelve cuántas quedaron
    /// confirmadas.
    /// </summary>
    public class AckApiSunatCredentialsCommand : IRequest<Result<ApiSunatAckResponseDto>>
    {
        /// <summary>Clave de la cabecera <c>X-License-Key</c>.</summary>
        public string? LicenseKey { get; set; }
        public ApiSunatAckRequestDto? ApiSunatAckRequestDto { get; set; }
    }

    internal class AckApiSunatCredentialsCommandHandler(
        IUnitOfWork<int> unitOfWork,
        IDateTimeService dateTime) : IRequestHandler<AckApiSunatCredentialsCommand, Result<ApiSunatAckResponseDto>>
    {
        public const string RetryMessage = "Las entregas cambiaron mientras se confirmaban: vuelva a intentarlo.";

        public async Task<Result<ApiSunatAckResponseDto>> Handle(AckApiSunatCredentialsCommand command, CancellationToken cancellationToken)
        {
            var key = command.LicenseKey?.Trim();
            if (string.IsNullOrEmpty(key) || key.Length > LicenseKeys.MaxLength)
            {
                return await Result<ApiSunatAckResponseDto>.FailAsync(CheckLicenseCommandHandler.InvalidKeyMessage);
            }
            var instance = await unitOfWork.ErpInstances.GetByKeyHashAsync(LicenseKeys.Hash(key), cancellationToken);
            if (instance is null || !instance.IsActive)
            {
                return await Result<ApiSunatAckResponseDto>.FailAsync(CheckLicenseCommandHandler.InvalidKeyMessage);
            }

            var ids = command.ApiSunatAckRequestDto!.Ids!.Distinct().ToList();
            var deliveries = await unitOfWork.ApiCredentialDeliveries.GetByIdsForInstanceAsync(instance.Id, ids, cancellationToken);

            var now = dateTime.NowUtc;
            var acknowledged = 0;
            var changed = false;
            foreach (var delivery in deliveries)
            {
                if (delivery.Status == ApiCredentialDeliveryStatus.Ready)
                {
                    CredentialSecrets.Wipe(delivery);
                    delivery.Status = ApiCredentialDeliveryStatus.Delivered;
                    delivery.DeliveredOnUtc = now;
                    delivery.LastError = null;
                    await unitOfWork.ApiCredentialDeliveries.UpdateAsync(delivery);
                    changed = true;
                    acknowledged++;
                }
                else if (delivery.Status == ApiCredentialDeliveryStatus.Delivered)
                {
                    acknowledged++;
                }
            }

            if (changed)
            {
                try
                {
                    await unitOfWork.Commit(cancellationToken);
                }
                catch (DbUpdateConcurrencyException)
                {
                    // Mientras tanto la cancelaron o venció: el ERP vuelve a consultar y verá el estado real.
                    unitOfWork.ClearChanges();
                    return await Result<ApiSunatAckResponseDto>.FailAsync(RetryMessage);
                }
            }
            return await Result<ApiSunatAckResponseDto>.SuccessAsync(new ApiSunatAckResponseDto { Acknowledged = acknowledged });
        }
    }
}
