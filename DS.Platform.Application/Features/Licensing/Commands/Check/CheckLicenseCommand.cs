using DS.Platform.Application.DTOs.Instances.Request;
using DS.Platform.Application.DTOs.Instances.Response;
using DS.Platform.Application.Helpers;
using DS.Platform.Application.Features.Instances.Common;
using DS.Platform.Application.Interfaces.Repositories;
using DS.Platform.Application.Interfaces.Services;
using DS.Platform.Application.Interfaces.Services.Licensing;
using DS.Platform.Application.Mappings.Instances;
using DS.Platform.Domain.Entities.Erp;
using DS.Platform.Shared.Wrapper;
using MediatR;
using System.Text;

namespace DS.Platform.Application.Features.Licensing.Commands.Check
{
    /// <summary>
    /// Consulta de licencia de una instancia del ERP (cada 10 min, con su clave). Devuelve la licencia firmada con Ed25519 y registra
    /// el contacto y el uso que informa. Es el único caso de uso de la consola expuesto a internet: una clave desconocida o inactiva
    /// recibe siempre el mismo mensaje (no revela si existe) y el controlador limita las consultas por IP.
    /// </summary>
    public class CheckLicenseCommand : IRequest<Result<LicenseEnvelopeDto>>
    {
        /// <summary>Clave de la cabecera <c>X-License-Key</c>.</summary>
        public string? LicenseKey { get; set; }
        public LicenseCheckRequestDto? LicenseCheckRequestDto { get; set; }
    }

    internal class CheckLicenseCommandHandler(
        IUnitOfWork<int> unitOfWork,
        IDateTimeService dateTime,
        ILicenseSigner signer) : IRequestHandler<CheckLicenseCommand, Result<LicenseEnvelopeDto>>
    {
        public const string InvalidKeyMessage = "Licencia no válida.";
        public const string NotConfiguredMessage = "El servicio de licencias no está disponible.";

        public async Task<Result<LicenseEnvelopeDto>> Handle(CheckLicenseCommand command, CancellationToken cancellationToken)
        {
            if (!signer.IsConfigured)
            {
                return await Result<LicenseEnvelopeDto>.FailAsync(NotConfiguredMessage);
            }

            var key = command.LicenseKey?.Trim();
            if (string.IsNullOrEmpty(key) || key.Length > LicenseKeys.MaxLength)
            {
                return await Result<LicenseEnvelopeDto>.FailAsync(InvalidKeyMessage);
            }
            var instance = await unitOfWork.ErpInstances.GetByKeyHashAsync(LicenseKeys.Hash(key), cancellationToken);
            if (instance is null || !instance.IsActive)
            {
                return await Result<LicenseEnvelopeDto>.FailAsync(InvalidKeyMessage);
            }

            var now = dateTime.NowUtc;
            var report = command.LicenseCheckRequestDto!;
            var subscriptions = await unitOfWork.Subscriptions.GetErpWithPlanByCustomerAsync(instance.CustomerId, cancellationToken);
            var state = LicenseStateBuilder.Build(subscriptions, LimaDate.Today(now));

            var payload = LicensePayload.Build(state, report.Nonce!, now);
            var envelope = new LicenseEnvelopeDto { Payload = payload, Signature = signer.Sign(Encoding.UTF8.GetBytes(payload)) };

            report.ApplyTo(instance, now);
            await unitOfWork.ErpInstances.UpdateAsync(instance);
            await RecordIssueIfChangedAsync(instance, state, now, cancellationToken);
            await unitOfWork.Commit(cancellationToken);

            return await Result<LicenseEnvelopeDto>.SuccessAsync(envelope);
        }

        /// <summary>Una fila del historial solo cuando cambia lo que se entrega (estado, plan o fechas), no en cada consulta.</summary>
        private async Task RecordIssueIfChangedAsync(ErpInstance instance, LicenseState state, DateTime now, CancellationToken cancellationToken)
        {
            var last = await unitOfWork.LicenseIssues.GetLastAsync(instance.Id, cancellationToken);
            if (last is not null && last.Status == state.Status && last.PlanCode == state.PlanCode
                && last.CoveredUntil == state.CoveredUntil && last.SuspendOn == state.SuspendOn)
            {
                return;
            }
            await unitOfWork.LicenseIssues.AddAsync(new LicenseIssue
            {
                InstanceId = instance.Id,
                IssuedOnUtc = now,
                Status = state.Status,
                PlanCode = state.PlanCode,
                CoveredUntil = state.CoveredUntil,
                SuspendOn = state.SuspendOn,
                IsActive = true
            });
        }
    }
}
