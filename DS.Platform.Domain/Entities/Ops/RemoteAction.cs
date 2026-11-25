using DS.Platform.Domain.Contracts;
using DS.Platform.Domain.Entities.ApiSunat;
using DS.Platform.Domain.Enums;
using System;

namespace DS.Platform.Domain.Entities.Ops
{
    /// <summary>
    /// Bandeja de acciones remotas (diseño §5.1): la llamada a un sistema externo nunca va dentro del caso de uso. El handler
    /// guarda el cambio local y esta fila en el mismo <c>Commit</c>; el job <c>RemoteActionsDispatch</c> la envía con reintentos.
    /// Así, si la API SUNAT está caída, el pago queda registrado y la reactivación llega sola cuando vuelve.
    /// </summary>
    public class RemoteAction : AuditableEntity<int>, IConcurrencyChecked
    {
        public required RemoteActionType Type { get; set; }
        public required int IssuerId { get; set; }
        public required int CustomerId { get; set; }

        /// <summary>Motivo que viaja a la API y queda en su auditoría.</summary>
        public required string Reason { get; set; }
        // Quién la originó es CreatedBy (usuario de la consola; vacío si la creó el job diario, y entonces va "sistema"):
        // el despachador lo envía como X-Admin-Actor.
        /// <summary>Solo <c>ApplyPlan</c>: el plan de la API (free / pro / business) que se aplica.</summary>
        public string? PlanSlug { get; set; }
        /// <summary>Solo <c>ApplyPlan</c>: cupo mensual; null = el del plan.</summary>
        public int? PlanMaxDocuments { get; set; }
        /// <summary>Único: evita encolar dos veces la misma acción.</summary>
        public required string IdempotencyKey { get; set; }

        public RemoteActionStatus Status { get; set; } = RemoteActionStatus.Pending;
        public int Attempts { get; set; }
        public DateTime NextAttemptOnUtc { get; set; }
        /// <summary>Mientras está Enviando: hasta cuándo es suya. Pasada esa hora otro despachador puede tomarla (si el proceso murió).</summary>
        public DateTime? LockedUntilUtc { get; set; }
        public string? LastError { get; set; }
        public DateTime? DoneOnUtc { get; set; }

        public virtual ApiSunatIssuer? Issuer { get; set; }
    }
}
