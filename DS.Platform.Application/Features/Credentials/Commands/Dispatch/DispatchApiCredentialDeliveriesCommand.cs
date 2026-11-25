using DS.Platform.Application.Features.Credentials.Common;
using DS.Platform.Application.Features.RemoteActions.Common;
using DS.Platform.Application.Interfaces.Repositories;
using DS.Platform.Application.Interfaces.Services;
using DS.Platform.Application.Interfaces.Services.ApiSunat;
using DS.Platform.Application.Interfaces.Services.Security;
using DS.Platform.Domain.Entities.Ops;
using DS.Platform.Domain.Enums;
using DS.Platform.Shared.Wrapper;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace DS.Platform.Application.Features.Credentials.Commands.Dispatch
{
    /// <summary>
    /// Genera en la API SUNAT las credenciales de las entregas que tocan (fase C6b) y borra el secreto de las que vencieron. Lo envía el
    /// job <c>ApiCredentialsDispatch</c> cada minuto. Igual que el despachador de accesos al portal: toma un lote marcándolo En curso en
    /// un guardado propio (si otro despachador lo tomó antes, el guardado choca por <c>xmin</c> y este no hace nada: nunca se regenera
    /// dos veces por la misma entrega) y cada entrega guarda su resultado por separado.
    /// La API key y el secret viajan de la respuesta de la API a la columna cifrada (<c>ISecretProtector</c>) en el mismo guardado:
    /// no se registran, no se devuelven y se borran al confirmar el ERP, al vencer o al cancelar. Sin la dirección pública de la API
    /// no se regenera nada (invalidaría las credenciales actuales sin poder entregar las nuevas). Devuelve cuántas quedaron Listas.
    /// </summary>
    public class DispatchApiCredentialDeliveriesCommand : IRequest<Result<int>>
    {
        /// <summary>Máximo de entregas por pasada.</summary>
        public int Batch { get; set; } = 10;
    }

    internal class DispatchApiCredentialDeliveriesCommandHandler(
        IUnitOfWork<int> unitOfWork,
        IApiSunatAdminClient apiSunat,
        IApiSunatSettings settings,
        ISecretProtector protector,
        IDateTimeService dateTime) : IRequestHandler<DispatchApiCredentialDeliveriesCommand, Result<int>>
    {
        public const string SystemActor = "sistema";
        public const string ExpiredMessage = "Las credenciales esperaron 7 días sin que el ERP las recogiera: entregue de nuevo.";

        public async Task<Result<int>> Handle(DispatchApiCredentialDeliveriesCommand request, CancellationToken cancellationToken)
        {
            var now = dateTime.NowUtc;
            await ExpireAsync(now, cancellationToken);

            var due = await unitOfWork.ApiCredentialDeliveries.GetDueAsync(now, Math.Clamp(request.Batch, 1, 50), cancellationToken);
            if (due.Count == 0)
            {
                return await Result<int>.SuccessAsync(0, "No hay entregas de credenciales pendientes.");
            }

            foreach (var delivery in due)
            {
                delivery.Status = ApiCredentialDeliveryStatus.Sending;
                delivery.LockedUntilUtc = now + RemoteActionRetryPolicy.Lease;
            }
            try
            {
                await unitOfWork.Commit(cancellationToken);
            }
            catch (DbUpdateConcurrencyException)
            {
                unitOfWork.ClearChanges();
                return await Result<int>.SuccessAsync(0, "Otro despachador tomó las entregas.");
            }

            var ready = 0;
            foreach (var delivery in due)
            {
                if (await ProcessAsync(delivery, cancellationToken))
                {
                    ready++;
                }
            }
            return await Result<int>.SuccessAsync(ready, $"{ready} entregas de credenciales listas.");
        }

        /// <summary>Las Listas que nadie recogió en 7 días pierden su secreto y pasan a Vencida.</summary>
        private async Task ExpireAsync(DateTime now, CancellationToken cancellationToken)
        {
            var expired = await unitOfWork.ApiCredentialDeliveries.GetExpiredReadyAsync(now, 50, cancellationToken);
            if (expired.Count == 0)
            {
                return;
            }
            foreach (var delivery in expired)
            {
                CredentialSecrets.Wipe(delivery);
                delivery.Status = ApiCredentialDeliveryStatus.Expired;
                delivery.LastError = ExpiredMessage;
            }
            try
            {
                await unitOfWork.Commit(cancellationToken);
            }
            catch (DbUpdateConcurrencyException)
            {
                // Mientras tanto el ERP la confirmó (o la cancelaron): vale lo que hay en la base.
                unitOfWork.ClearChanges();
            }
        }

        private async Task<bool> ProcessAsync(ApiCredentialDelivery delivery, CancellationToken cancellationToken)
        {
            var ready = false;
            try
            {
                if (!settings.IsPublicUrlConfigured)
                {
                    // Sin dirección pública no se regenera nada: invalidaría las credenciales actuales sin poder entregar las nuevas.
                    Fail(delivery, CredentialDeliveryRules.NotConfiguredMessage, retryable: true);
                }
                else
                {
                    var actor = string.IsNullOrWhiteSpace(delivery.LastModifiedBy) ? delivery.CreatedBy : delivery.LastModifiedBy;
                    actor = string.IsNullOrWhiteSpace(actor) ? SystemActor : actor;

                    var call = await apiSunat.RegenerateTenantCredentialsAsync(
                        delivery.TenantId, CredentialDeliveryRules.ReasonFor(delivery.InstanceId), actor, cancellationToken);
                    delivery.Attempts++;
                    delivery.LockedUntilUtc = null;
                    if (call.Success)
                    {
                        // Las credenciales pasan de la respuesta a la columna cifrada en este mismo guardado; nada más las conserva.
                        var now = dateTime.NowUtc;
                        delivery.ApiKeyProtected = protector.Protect(call.Data!.ApiKey);
                        delivery.ApiSecretProtected = protector.Protect(call.Data.ApiSecret);
                        delivery.Status = ApiCredentialDeliveryStatus.Ready;
                        delivery.ReadyOnUtc = now;
                        delivery.ExpiresOnUtc = now + CredentialDeliveryRules.ReadyValidity;
                        delivery.LastError = null;
                        ready = true;
                    }
                    else
                    {
                        Fail(delivery, call.Error ?? $"HTTP {call.StatusCode}", call.Retryable, countAttempt: false);
                    }
                }

                await unitOfWork.ApiCredentialDeliveries.UpdateAsync(delivery);
                await unitOfWork.Commit(cancellationToken);
            }
            catch (DbUpdateConcurrencyException)
            {
                // Mientras se ejecutaba, alguien la tocó: vale lo que hay en la base. (Una entrega En curso no se puede cancelar,
                // así que esto solo ocurre si otro despachador retomó un bloqueo vencido.)
                unitOfWork.ClearChanges();
                return false;
            }
            return ready;
        }

        private void Fail(ApiCredentialDelivery delivery, string error, bool retryable, bool countAttempt = true)
        {
            if (countAttempt)
            {
                delivery.Attempts++;
            }
            delivery.LockedUntilUtc = null;
            delivery.LastError = error.Length > 1000 ? error[..1000] : error;
            if (!retryable || RemoteActionRetryPolicy.IsExhausted(delivery.Attempts))
            {
                delivery.Status = ApiCredentialDeliveryStatus.Abandoned;
            }
            else
            {
                delivery.Status = ApiCredentialDeliveryStatus.Failed;
                delivery.NextAttemptOnUtc = dateTime.NowUtc + RemoteActionRetryPolicy.DelayAfter(delivery.Attempts);
            }
        }
    }
}
