using DS.Platform.Application.Features.ApiSunat.Common;
using DS.Platform.Application.Features.RemoteActions.Common;
using DS.Platform.Application.Interfaces.Repositories;
using DS.Platform.Application.Interfaces.Services;
using DS.Platform.Application.Interfaces.Services.ApiSunat;
using DS.Platform.Application.Mappings.ApiSunat;
using DS.Platform.Domain.Entities.Ops;
using DS.Platform.Domain.Enums;
using DS.Platform.Shared.Wrapper;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace DS.Platform.Application.Features.RemoteActions.Commands.Dispatch
{
    /// <summary>
    /// Envía a la API SUNAT las acciones remotas que toca (diseño §5.1). Lo envía el job <c>RemoteActionsDispatch</c> cada minuto.
    /// Primero toma un lote y lo marca Enviando en un guardado propio; si otro despachador lo tomó antes, el guardado choca por
    /// <c>xmin</c> y este no envía nada (nunca se envía dos veces). Después cada acción guarda su resultado por separado, así una
    /// caída a mitad del lote no pierde lo ya hecho. Devuelve cuántas se aplicaron.
    /// </summary>
    public class DispatchRemoteActionsCommand : IRequest<Result<int>>
    {
        /// <summary>Máximo de acciones por pasada.</summary>
        public int Batch { get; set; } = 20;
    }

    internal class DispatchRemoteActionsCommandHandler(
        IUnitOfWork<int> unitOfWork,
        IApiSunatAdminClient apiSunat,
        IDateTimeService dateTime) : IRequestHandler<DispatchRemoteActionsCommand, Result<int>>
    {
        public const string IssuerGoneMessage = "El emisor ya no existe en la consola.";
        public const string StaleMessage = "Ya no aplica: el estado deseado del emisor cambió.";
        public const string SystemActor = "sistema";

        public async Task<Result<int>> Handle(DispatchRemoteActionsCommand request, CancellationToken cancellationToken)
        {
            var now = dateTime.NowUtc;
            var due = await unitOfWork.RemoteActions.GetDueAsync(now, Math.Clamp(request.Batch, 1, 100), cancellationToken);
            if (due.Count == 0)
            {
                return await Result<int>.SuccessAsync(0, "No hay acciones pendientes.");
            }

            foreach (var action in due)
            {
                action.Status = RemoteActionStatus.Sending;
                action.LockedUntilUtc = now + RemoteActionRetryPolicy.Lease;
            }
            try
            {
                await unitOfWork.Commit(cancellationToken);
            }
            catch (DbUpdateConcurrencyException)
            {
                // Otro despachador tomó alguna: que se ocupe él.
                unitOfWork.ClearChanges();
                return await Result<int>.SuccessAsync(0, "Otro despachador tomó las acciones.");
            }

            var done = 0;
            foreach (var action in due)
            {
                if (await ProcessAsync(action, cancellationToken))
                {
                    done++;
                }
            }
            return await Result<int>.SuccessAsync(done, $"{done} acciones aplicadas.");
        }

        private async Task<bool> ProcessAsync(RemoteAction action, CancellationToken cancellationToken)
        {
            var applied = false;
            var issuer = await unitOfWork.ApiSunatIssuers.GetByIdAsync(action.IssuerId);
            var suspend = action.Type == RemoteActionType.SuspendIssuer;
            var isPlan = action.Type == RemoteActionType.ApplyPlan;

            if (issuer is null || issuer.IsDeleted)
            {
                Finish(action, RemoteActionStatus.Abandoned, IssuerGoneMessage);
            }
            else if (isPlan
                ? issuer.PlanDesiredSlug != action.PlanSlug || issuer.PlanDesiredMaxDocuments != action.PlanMaxDocuments
                : issuer.SuspendDesired != suspend)
            {
                // Otro cambio posterior ya lo dejó sin efecto (el reconciliador debió cancelarla; esto es la red de seguridad).
                Finish(action, RemoteActionStatus.Cancelled, StaleMessage);
            }
            else
            {
                var actor = string.IsNullOrWhiteSpace(action.CreatedBy) ? SystemActor : action.CreatedBy;
                var call = isPlan
                    ? await apiSunat.SetPlanAsync(issuer.TenantId, action.PlanSlug!, action.PlanMaxDocuments, action.Reason, actor, cancellationToken)
                    : await apiSunat.SetBillingSuspensionAsync(issuer.TenantId, suspend, action.Reason, actor, cancellationToken);
                if (call.Success)
                {
                    var tenant = call.Data!;
                    var now = dateTime.NowUtc;
                    action.Attempts++;
                    Finish(action, RemoteActionStatus.Done, null);
                    action.DoneOnUtc = now;
                    tenant.ApplyTo(issuer, now);
                    if (!isPlan)
                    {
                        issuer.SuspendApplied = ApiAccessPolicy.AppliedFor(tenant, suspend);
                    }
                    await unitOfWork.ApiSunatIssuers.UpdateAsync(issuer);
                    applied = true;
                }
                else
                {
                    action.Attempts++;
                    var error = call.Error ?? $"HTTP {call.StatusCode}";
                    action.LastError = error.Length > 1000 ? error[..1000] : error;
                    if (!call.Retryable || RemoteActionRetryPolicy.IsExhausted(action.Attempts))
                    {
                        action.Status = RemoteActionStatus.Abandoned;
                        action.LockedUntilUtc = null;
                    }
                    else
                    {
                        action.Status = RemoteActionStatus.Failed;
                        action.LockedUntilUtc = null;
                        action.NextAttemptOnUtc = dateTime.NowUtc + RemoteActionRetryPolicy.DelayAfter(action.Attempts);
                    }
                }
            }

            await unitOfWork.RemoteActions.UpdateAsync(action);
            try
            {
                await unitOfWork.Commit(cancellationToken);
            }
            catch (DbUpdateConcurrencyException)
            {
                // Mientras se enviaba, alguien la cambió (p. ej. la cancelaron): lo que mande ahora es lo de la base.
                unitOfWork.ClearChanges();
                return false;
            }
            return applied;
        }

        private static void Finish(RemoteAction action, RemoteActionStatus status, string? error)
        {
            action.Status = status;
            action.LockedUntilUtc = null;
            action.LastError = error;
        }
    }
}
