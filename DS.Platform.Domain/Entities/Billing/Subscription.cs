using DS.Platform.Domain.Contracts;
using DS.Platform.Domain.Entities.Customers;
using DS.Platform.Domain.Enums;
using System;

namespace DS.Platform.Domain.Entities.Billing
{
    /// <summary>
    /// Qué cliente tiene qué plan. Un cliente tiene como mucho una suscripción no cancelada por producto (ERP / API SUNAT).
    /// Las fechas son de Lima (DateOnly). <see cref="CoveredUntil"/>, <see cref="SuspendOn"/> y <see cref="Status"/> los
    /// recalcula <c>SubscriptionStatusCalculator</c> en cada cambio y en el job diario.
    /// </summary>
    public class Subscription : AuditableEntity<int>, IConcurrencyChecked
    {
        public required int CustomerId { get; set; }
        public required int PlanId { get; set; }
        /// <summary>Copia del producto del plan (para el índice "una por producto" y los filtros).</summary>
        public required PlanProduct Product { get; set; }

        public BillingCycle BillingCycle { get; set; } = BillingCycle.Monthly;
        /// <summary>Precio pactado por ciclo (sin IGV); nace con el del plan y puede tener descuento.</summary>
        public decimal Price { get; set; }
        public string Currency { get; set; } = "PEN";

        public DateOnly StartOn { get; set; }
        /// <summary>Último día de la prueba gratuita; null = sin prueba (solo la primera suscripción del producto la tiene).</summary>
        public DateOnly? TrialEndsOn { get; set; }
        /// <summary>Último día pagado (lo extienden los pagos y las prórrogas); null = aún no paga.</summary>
        public DateOnly? PaidUntil { get; set; }
        /// <summary>Día del mes en que vence cada periodo (1-28).</summary>
        public int BillingDay { get; set; }
        /// <summary>Días después del vencimiento en que todavía se entra (con aviso) antes de suspender.</summary>
        public int GraceDays { get; set; } = 5;

        /// <summary>Último día cubierto: el mayor entre la prueba y lo pagado (o el día anterior al inicio si no hay ninguno).</summary>
        public DateOnly CoveredUntil { get; set; }
        /// <summary>Día en que se suspende si no paga: <see cref="CoveredUntil"/> + <see cref="GraceDays"/> + 1.</summary>
        public DateOnly? SuspendOn { get; set; }
        public SubscriptionStatus Status { get; set; } = SubscriptionStatus.Trial;

        /// <summary>Suspendida a mano (con motivo), aunque esté al día.</summary>
        public bool SuspendedManually { get; set; }
        /// <summary>Motivo de la última suspensión, prórroga, reactivación o cancelación.</summary>
        public string? StatusReason { get; set; }
        public DateOnly? CancelledOn { get; set; }
        public string? Notes { get; set; }

        public virtual Customer? Customer { get; set; }
        public virtual Plan? Plan { get; set; }
    }
}
