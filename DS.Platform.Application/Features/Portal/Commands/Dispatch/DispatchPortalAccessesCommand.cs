using DS.Platform.Application.Features.Notifications.Common;
using DS.Platform.Application.Features.Portal.Common;
using DS.Platform.Application.Features.RemoteActions.Common;
using DS.Platform.Application.Interfaces.Repositories;
using DS.Platform.Application.Interfaces.Services;
using DS.Platform.Application.Interfaces.Services.ApiSunat;
using DS.Platform.Application.Interfaces.Services.Portal;
using DS.Platform.Domain.Entities.ApiSunat;
using DS.Platform.Domain.Entities.Customers;
using DS.Platform.Domain.Entities.Ops;
using DS.Platform.Domain.Enums;
using DS.Platform.Shared.Wrapper;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace DS.Platform.Application.Features.Portal.Commands.Dispatch
{
    /// <summary>
    /// Cumple en la API SUNAT los accesos al portal que tocan (fase C6): invitar (crear al usuario y recibir su token de activación) y
    /// habilitar / deshabilitar. Lo envía el job <c>PortalAccessDispatch</c> cada minuto. Igual que el despachador de acciones remotas:
    /// toma un lote marcándolo En curso en un guardado propio (si otro despachador lo tomó antes, el guardado choca por <c>xmin</c> y
    /// este no hace nada: una invitación nunca sale dos veces) y cada acceso guarda su resultado por separado.
    /// El token de activación viaja de la respuesta de la API al cuerpo del aviso por correo en el mismo guardado: no se registra, no
    /// se devuelve y el cuerpo lo reemplaza un texto sin enlace en cuanto el aviso sale (<c>SendNotificationsCommand</c>).
    /// Devuelve cuántos terminaron.
    /// </summary>
    public class DispatchPortalAccessesCommand : IRequest<Result<int>>
    {
        /// <summary>Máximo de accesos por pasada.</summary>
        public int Batch { get; set; } = 10;
    }

    internal class DispatchPortalAccessesCommandHandler(
        IUnitOfWork<int> unitOfWork,
        IApiSunatAdminClient apiSunat,
        IPortalSettings settings,
        IDateTimeService dateTime) : IRequestHandler<DispatchPortalAccessesCommand, Result<int>>
    {
        public const string SystemActor = "sistema";
        public const string NoIssuersMessage = "Las empresas de este acceso ya no existen en la consola: invite de nuevo y elija otras.";
        public const string NoApiUserMessage = "La persona no tiene usuario en el portal: invítela de nuevo.";

        private sealed record Failure(string Error, bool Retryable);

        public async Task<Result<int>> Handle(DispatchPortalAccessesCommand request, CancellationToken cancellationToken)
        {
            var now = dateTime.NowUtc;
            var due = await unitOfWork.PortalAccesses.GetDueAsync(now, Math.Clamp(request.Batch, 1, 50), cancellationToken);
            if (due.Count == 0)
            {
                return await Result<int>.SuccessAsync(0, "No hay accesos al portal pendientes.");
            }

            foreach (var access in due)
            {
                access.Status = RemoteActionStatus.Sending;
                access.LockedUntilUtc = now + RemoteActionRetryPolicy.Lease;
            }
            try
            {
                await unitOfWork.Commit(cancellationToken);
            }
            catch (DbUpdateConcurrencyException)
            {
                unitOfWork.ClearChanges();
                return await Result<int>.SuccessAsync(0, "Otro despachador tomó los accesos.");
            }

            // Empresas y clientes de todo el lote, en dos consultas (sin consultar dentro del bucle).
            var issuerIds = due.SelectMany(a => a.Issuers.Select(i => i.IssuerId)).Distinct().ToList();
            var issuers = (await unitOfWork.Repository<ApiSunatIssuer>().GetAllQueryable()
                .Where(i => issuerIds.Contains(i.Id)).ToListAsync(cancellationToken)).ToDictionary(i => i.Id);
            var customerIds = due.Select(a => a.CustomerId).Distinct().ToList();
            var customers = (await unitOfWork.Repository<Customer>().GetAllQueryable()
                .Where(c => customerIds.Contains(c.Id)).ToListAsync(cancellationToken)).ToDictionary(c => c.Id);

            var done = 0;
            foreach (var access in due)
            {
                if (await ProcessAsync(access, issuers, customers, cancellationToken))
                {
                    done++;
                }
            }
            return await Result<int>.SuccessAsync(done, $"{done} accesos al portal terminados.");
        }

        private async Task<bool> ProcessAsync(
            PortalAccess access, Dictionary<int, ApiSunatIssuer> issuers, Dictionary<int, Customer> customers, CancellationToken cancellationToken)
        {
            var finished = false;
            try
            {
                var actor = string.IsNullOrWhiteSpace(access.LastModifiedBy) ? access.CreatedBy : access.LastModifiedBy;
                actor = string.IsNullOrWhiteSpace(actor) ? SystemActor : actor;

                var failure = access.WorkType == PortalWorkType.Invite
                    ? await InviteAsync(access, issuers, customers, actor, cancellationToken)
                    : await SetStateAsync(access, actor, cancellationToken);

                access.Attempts++;
                access.LockedUntilUtc = null;
                if (failure is null)
                {
                    access.Status = RemoteActionStatus.Done;
                    access.LastError = null;
                    access.DoneOnUtc = dateTime.NowUtc;
                    finished = true;
                }
                else
                {
                    access.LastError = failure.Error.Length > 1000 ? failure.Error[..1000] : failure.Error;
                    if (!failure.Retryable || RemoteActionRetryPolicy.IsExhausted(access.Attempts))
                    {
                        access.Status = RemoteActionStatus.Abandoned;
                    }
                    else
                    {
                        access.Status = RemoteActionStatus.Failed;
                        access.NextAttemptOnUtc = dateTime.NowUtc + RemoteActionRetryPolicy.DelayAfter(access.Attempts);
                    }
                }

                await unitOfWork.PortalAccesses.UpdateAsync(access);
                await unitOfWork.Commit(cancellationToken);
            }
            catch (DbUpdateConcurrencyException)
            {
                // Mientras se ejecutaba, alguien lo cambió (p. ej. lo deshabilitaron): vale lo que hay en la base.
                unitOfWork.ClearChanges();
                return false;
            }
            return finished;
        }

        private async Task<Failure?> InviteAsync(
            PortalAccess access, Dictionary<int, ApiSunatIssuer> issuers, Dictionary<int, Customer> customers, string actor, CancellationToken cancellationToken)
        {
            if (!settings.IsConfigured)
            {
                return new Failure(PortalRules.NotConfiguredMessage, true);
            }
            var linked = access.Issuers.Select(i => issuers.GetValueOrDefault(i.IssuerId)).OfType<ApiSunatIssuer>().ToList();
            if (linked.Count == 0 || !customers.TryGetValue(access.CustomerId, out var customer))
            {
                return new Failure(NoIssuersMessage, false);
            }

            var call = await apiSunat.CreatePortalInvitationAsync(
                access.Email, access.Name, linked.Select(i => i.TenantId).ToList(), access.ResetMfa, actor, cancellationToken);
            if (!call.Success)
            {
                return new Failure(call.Error ?? $"HTTP {call.StatusCode}", call.Retryable);
            }

            var invitation = call.Data!;
            var now = dateTime.NowUtc;
            access.ApiUserId = invitation.UserId;
            access.InvitedOnUtc = now;
            access.InvitationExpiresOnUtc = invitation.ExpiresAtUtc;
            access.InvitationCount++;
            access.ResetMfa = false;

            // El token pasa de la respuesta al cuerpo del aviso en el mismo guardado; nada más lo conserva.
            var validHours = Math.Max(1, (int)Math.Round((invitation.ExpiresAtUtc - now).TotalHours));
            var (subject, body) = NoticeTemplates.PortalInvitationEmail(new PortalInvitationNotice(
                access.Name, customer.BusinessName, PortalRules.ActivationUrl(settings.PublicUrl!, invitation.Token), validHours,
                linked.Select(i => $"{i.BusinessName} (RUC {i.Ruc})").ToList()));
            await NotificationOutbox.EnqueueAsync(unitOfWork, [
                new NoticeSpec(NotificationType.PortalInvitation, customer, $"portal-invite:{access.Id}:{access.InvitationCount}",
                    subject, body, ToEmail: access.Email)], now, cancellationToken);
            return null;
        }

        private async Task<Failure?> SetStateAsync(PortalAccess access, string actor, CancellationToken cancellationToken)
        {
            if (access.ApiUserId is not long userId)
            {
                return new Failure(NoApiUserMessage, false);
            }
            var call = await apiSunat.SetPortalUserActiveAsync(userId, access.IsEnabled, actor, cancellationToken);
            return call.Success ? null : new Failure(call.Error ?? $"HTTP {call.StatusCode}", call.Retryable);
        }
    }
}
