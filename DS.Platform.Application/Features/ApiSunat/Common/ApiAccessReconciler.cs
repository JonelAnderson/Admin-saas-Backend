using DS.Platform.Application.Interfaces.Repositories;
using DS.Platform.Application.Interfaces.Services.ApiSunat;
using DS.Platform.Domain.Entities.ApiSunat;
using DS.Platform.Domain.Entities.Billing;
using DS.Platform.Domain.Entities.Ops;
using DS.Platform.Domain.Enums;

namespace DS.Platform.Application.Features.ApiSunat.Common
{
    /// <summary>
    /// Regla pura: ¿la emisión de las empresas de la API SUNAT de un cliente debe estar suspendida?
    /// La API SUNAT va incluida en el plan del ERP; si el cliente la contrata sola tiene su propia suscripción. Por eso basta
    /// una suscripción vigente (Prueba, Activa o En gracia) de cualquier producto para que pueda emitir. Si tiene suscripciones
    /// pero ninguna vigente (suspendidas por falta de pago o a mano, o canceladas) se suspende. Sin ninguna suscripción el cliente
    /// no se administra desde la consola y no se toca (<c>null</c>).
    /// </summary>
    public static class ApiAccessPolicy
    {
        public static bool? ShouldSuspend(IReadOnlyCollection<Subscription> subscriptions)
        {
            var own = subscriptions.Where(s => !s.IsDeleted).ToList();
            if (own.Count == 0)
            {
                return null;
            }
            return !own.Any(s => s.Status is SubscriptionStatus.Trial or SubscriptionStatus.Active or SubscriptionStatus.Grace);
        }

        /// <summary>Motivo que viaja a la API SUNAT y queda en su auditoría.</summary>
        public static string ReasonFor(IReadOnlyCollection<Subscription> subscriptions, bool suspend)
        {
            if (!suspend)
            {
                return "Suscripción vigente.";
            }
            var own = subscriptions.Where(s => !s.IsDeleted).ToList();
            return own.All(s => s.Status == SubscriptionStatus.Cancelled)
                ? "Suscripción cancelada."
                : "Suscripción suspendida por falta de pago o a mano en la consola.";
        }

        /// <summary>
        /// Lo que la API SUNAT tiene aplicado, visto desde lo que la consola pidió. Una suspensión manual del panel de la API
        /// (empresa inactiva) ya impide emitir, así que cuenta como suspendida cuando se pidió suspender; y la consola nunca
        /// la reactiva, así que al pedir reactivar solo cuenta la suspensión por pago.
        /// </summary>
        public static bool AppliedFor(ApiTenantInfo tenant, bool desiredSuspended)
            => desiredSuspended ? tenant.BillingSuspended || !tenant.IsActive : tenant.BillingSuspended;
    }

    /// <summary>
    /// Regla pura: ¿qué plan de la API SUNAT corresponde a las empresas de un cliente? Entre las suscripciones vigentes (Prueba,
    /// Activa o En gracia) cuyo plan indica un plan de la API (<c>ApiPlanSlug</c>: planes de la API sola o del ERP con API incluida),
    /// manda el de mayor nivel (business &gt; pro &gt; free) y, a igual nivel, el de mayor cupo (sin cupo = el del plan). Sin ninguna
    /// suscripción vigente con API no se toca el plan (<c>null</c>): una suspensión bloquea la emisión pero no degrada el plan.
    /// </summary>
    public static class ApiPlanPolicy
    {
        public sealed record Desired(string Slug, int? MaxDocuments);

        public static Desired? For(IReadOnlyCollection<Subscription> subscriptions)
        {
            var candidates = subscriptions
                .Where(s => !s.IsDeleted && s.Status is SubscriptionStatus.Trial or SubscriptionStatus.Active or SubscriptionStatus.Grace)
                .Select(s => s.Plan)
                .Where(p => p is not null && !string.IsNullOrWhiteSpace(p.ApiPlanSlug))
                .Select(p => p!)
                .ToList();
            if (candidates.Count == 0)
            {
                return null;
            }
            var best = candidates
                .OrderByDescending(p => Rank(p.ApiPlanSlug!))
                // Sin cupo (null) vale más que cualquier número: el plan manda su cupo propio.
                .ThenByDescending(p => p.MaxDocumentsPerMonth ?? int.MaxValue)
                .ThenBy(p => p.Id)
                .First();
            return new Desired(best.ApiPlanSlug!.Trim().ToLowerInvariant(), best.MaxDocumentsPerMonth);
        }

        /// <summary>
        /// Cuántas empresas emisoras (RUC) permite el plan del cliente: entre sus suscripciones vigentes con API, el mayor límite; si alguna
        /// no lo tiene (null) no hay tope. <c>null</c> también si no tiene ninguna con API (sin plan de API no se limita aquí: el corte
        /// por falta de pago ya lo cubre <see cref="ApiAccessPolicy"/>). El cupo de comprobantes es por empresa y lo hace cumplir la API.
        /// </summary>
        public static int? IssuerLimit(IReadOnlyCollection<Subscription> subscriptions)
        {
            var plans = subscriptions
                .Where(s => !s.IsDeleted && s.Status is SubscriptionStatus.Trial or SubscriptionStatus.Active or SubscriptionStatus.Grace)
                .Select(s => s.Plan)
                .Where(p => p is not null && !string.IsNullOrWhiteSpace(p.ApiPlanSlug))
                .Select(p => p!)
                .ToList();
            if (plans.Count == 0 || plans.Any(p => p.MaxIssuers is null))
            {
                return null;
            }
            return plans.Max(p => p.MaxIssuers);
        }

        private static int Rank(string slug) => slug.Trim().ToLowerInvariant() switch { "business" => 3, "pro" => 2, "free" => 1, _ => 0 };

        /// <summary>¿La API ya tiene el plan (y el cupo, si se pidió uno)? Sin cupo pedido manda el del plan: solo se compara el plan.</summary>
        public static bool IsApplied(ApiSunatIssuer issuer, Desired desired)
            => string.Equals(issuer.ApiPlan, desired.Slug, StringComparison.OrdinalIgnoreCase)
               && (desired.MaxDocuments is null || issuer.MaxDocumentsMonth == desired.MaxDocuments);

        public static string ReasonFor(Desired desired)
            => desired.MaxDocuments is int max
                ? $"Plan de la suscripción: {desired.Slug}, {max} comprobantes al mes."
                : $"Plan de la suscripción: {desired.Slug}.";
    }

    /// <summary>
    /// Deja la bandeja de acciones remotas al día con el estado de las suscripciones de un cliente. Lo llaman, antes de su único
    /// <c>Commit</c>, todos los casos de uso que cambian el estado de una suscripción (alta, edición, prórroga, suspensión,
    /// reactivación, cancelación, pagos y el job diario): la llamada a la API SUNAT nunca va dentro del caso de uso (diseño §5.1).
    /// No hace <c>Commit</c>. Si nada cambió no hace nada, así que llamarlo de más es inocuo.
    /// </summary>
    internal static class ApiAccessReconciler
    {
        public static async Task ReconcileAsync(
            IUnitOfWork<int> unitOfWork, int customerId, DateTime nowUtc, CancellationToken cancellationToken, Subscription? pending = null)
        {
            var issuers = await unitOfWork.ApiSunatIssuers.GetByCustomerAsync(customerId, cancellationToken);
            if (issuers.Count == 0)
            {
                return;
            }

            // Con seguimiento: ve los cambios de la suscripción que el handler aún no guardó.
            var subscriptions = await unitOfWork.Subscriptions.GetByCustomerAsync(customerId, cancellationToken);
            if (pending is not null && !subscriptions.Contains(pending))
            {
                subscriptions.Add(pending);
            }
            foreach (var issuer in issuers)
            {
                await ReconcileIssuerAsync(unitOfWork, issuer, subscriptions, nowUtc, cancellationToken);
            }
        }

        /// <summary>
        /// Un emisor (también uno recién creado, aún sin Id). Si cambió lo deseado, lo pedido antes se cancela y se encola la acción
        /// nueva; si lo deseado no cambió pero la API no lo tiene aplicado y no hay nada en camino, se encola para converger.
        /// </summary>
        public static async Task ReconcileIssuerAsync(
            IUnitOfWork<int> unitOfWork, ApiSunatIssuer issuer, IReadOnlyCollection<Subscription> subscriptions, DateTime nowUtc,
            CancellationToken cancellationToken)
        {
            await ReconcileSuspensionAsync(unitOfWork, issuer, subscriptions, nowUtc, cancellationToken);
            await ReconcilePlanAsync(unitOfWork, issuer, subscriptions, nowUtc, cancellationToken);
        }

        /// <summary>Igual que la suspensión: lo deseado cambia → se cancela lo pedido antes y se encola el plan nuevo; si no cambió pero la API no lo tiene y nada está en camino, se encola.</summary>
        private static async Task ReconcilePlanAsync(
            IUnitOfWork<int> unitOfWork, ApiSunatIssuer issuer, IReadOnlyCollection<Subscription> subscriptions, DateTime nowUtc,
            CancellationToken cancellationToken)
        {
            var wanted = ApiPlanPolicy.For(subscriptions);
            if (wanted is null)
            {
                return;
            }

            var open = issuer.Id == 0
                ? new List<RemoteAction>()
                : (await unitOfWork.RemoteActions.GetOpenForIssuerAsync(issuer.Id, cancellationToken)).Where(a => a.Type == RemoteActionType.ApplyPlan).ToList();
            var desiredChanged = issuer.PlanDesiredSlug != wanted.Slug || issuer.PlanDesiredMaxDocuments != wanted.MaxDocuments;
            var needsAction = !ApiPlanPolicy.IsApplied(issuer, wanted);

            if (!desiredChanged && (!needsAction || open.Count > 0))
            {
                return;
            }

            issuer.PlanDesiredSlug = wanted.Slug;
            issuer.PlanDesiredMaxDocuments = wanted.MaxDocuments;
            if (issuer.Id != 0)
            {
                await unitOfWork.ApiSunatIssuers.UpdateAsync(issuer);
            }

            if (desiredChanged)
            {
                foreach (var previous in open)
                {
                    previous.Status = RemoteActionStatus.Cancelled;
                    previous.LockedUntilUtc = null;
                    previous.LastError = "Reemplazada por un cambio de plan posterior.";
                    await unitOfWork.RemoteActions.UpdateAsync(previous);
                }
            }
            if (!needsAction)
            {
                return;
            }

            await unitOfWork.RemoteActions.AddAsync(new RemoteAction
            {
                Type = RemoteActionType.ApplyPlan,
                Issuer = issuer.Id == 0 ? issuer : null,
                IssuerId = issuer.Id,
                CustomerId = issuer.CustomerId,
                Reason = ApiPlanPolicy.ReasonFor(wanted),
                PlanSlug = wanted.Slug,
                PlanMaxDocuments = wanted.MaxDocuments,
                IdempotencyKey = $"issuer-{(issuer.Id == 0 ? issuer.TenantId : issuer.Id)}-plan-{wanted.Slug}-{wanted.MaxDocuments}-{nowUtc.Ticks}",
                Status = RemoteActionStatus.Pending,
                NextAttemptOnUtc = nowUtc,
                IsActive = true
            });
        }

        private static async Task ReconcileSuspensionAsync(
            IUnitOfWork<int> unitOfWork, ApiSunatIssuer issuer, IReadOnlyCollection<Subscription> subscriptions, DateTime nowUtc,
            CancellationToken cancellationToken)
        {
            var wanted = ApiAccessPolicy.ShouldSuspend(subscriptions);
            if (wanted is null)
            {
                return;
            }
            var suspend = wanted.Value;

            var open = issuer.Id == 0
                ? new List<RemoteAction>()
                : (await unitOfWork.RemoteActions.GetOpenForIssuerAsync(issuer.Id, cancellationToken)).Where(a => a.Type != RemoteActionType.ApplyPlan).ToList();
            var desiredChanged = issuer.SuspendDesired != suspend;
            var needsAction = issuer.SuspendApplied != suspend;

            if (!desiredChanged && (!needsAction || open.Count > 0))
            {
                return;
            }

            issuer.SuspendDesired = suspend;
            if (issuer.Id != 0)
            {
                await unitOfWork.ApiSunatIssuers.UpdateAsync(issuer);
            }

            if (desiredChanged)
            {
                // Lo pedido antes queda sin efecto: manda el último estado deseado.
                foreach (var previous in open)
                {
                    previous.Status = RemoteActionStatus.Cancelled;
                    previous.LockedUntilUtc = null;
                    previous.LastError = "Reemplazada por un cambio de estado posterior.";
                    await unitOfWork.RemoteActions.UpdateAsync(previous);
                }
            }

            // Si la API ya está como se quiere (p. ej. se pidió suspender y antes de enviarse se reactivó), no hay nada que enviar.
            if (!needsAction)
            {
                return;
            }

            await unitOfWork.RemoteActions.AddAsync(new RemoteAction
            {
                Type = suspend ? RemoteActionType.SuspendIssuer : RemoteActionType.ReactivateIssuer,
                // Un emisor nuevo aún no tiene Id: EF lo completa por la propiedad de navegación al guardar.
                Issuer = issuer.Id == 0 ? issuer : null,
                IssuerId = issuer.Id,
                CustomerId = issuer.CustomerId,
                Reason = ApiAccessPolicy.ReasonFor(subscriptions, suspend),
                IdempotencyKey = $"issuer-{(issuer.Id == 0 ? issuer.TenantId : issuer.Id)}-{(suspend ? "suspend" : "reactivate")}-{nowUtc.Ticks}",
                Status = RemoteActionStatus.Pending,
                NextAttemptOnUtc = nowUtc,
                IsActive = true
            });
        }
    }
}
