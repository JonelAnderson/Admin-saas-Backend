using DS.Platform.Domain.Contracts;
using DS.Platform.Domain.Entities.Customers;
using DS.Platform.Domain.Enums;
using System;

namespace DS.Platform.Domain.Entities.Billing
{
    /// <summary>
    /// Pago de un cliente por una suscripción (un pago = una suscripción: si paga ERP y API SUNAT juntos, son dos pagos).
    /// <b>Inmutable:</b> no se edita; se anula con motivo, y solo el último pago registrado de su suscripción, porque al anular
    /// "pagado hasta" vuelve a <see cref="PreviousPaidUntil"/>.
    /// </summary>
    public class Payment : AuditableEntity<int>, IConcurrencyChecked
    {
        public required int SubscriptionId { get; set; }
        /// <summary>Copia del cliente de la suscripción (filtros y reportes por cliente).</summary>
        public required int CustomerId { get; set; }

        /// <summary>Fecha del pago (Lima).</summary>
        public DateOnly PaidOn { get; set; }
        public decimal Amount { get; set; }
        public string Currency { get; set; } = "PEN";
        /// <summary>Precio pactado × ciclos al registrar (referencia: el monto puede diferir por un descuento).</summary>
        public decimal ExpectedAmount { get; set; }
        public PaymentMethod Method { get; set; }
        /// <summary>Número de operación del banco / Yape / Plin; único por medio entre los pagos registrados.</summary>
        public string? OperationNumber { get; set; }

        /// <summary>Ciclos (meses o años según la suscripción) que cubre.</summary>
        public int Periods { get; set; } = 1;
        public DateOnly PeriodStart { get; set; }
        public DateOnly PeriodEnd { get; set; }
        /// <summary>"Pagado hasta" de la suscripción antes de este pago (se restaura al anularlo).</summary>
        public DateOnly? PreviousPaidUntil { get; set; }

        public PaymentStatus Status { get; set; } = PaymentStatus.Registered;
        public DateOnly? VoidedOn { get; set; }
        public string? VoidReason { get; set; }
        public string? Notes { get; set; }

        public virtual Subscription? Subscription { get; set; }
        public virtual Customer? Customer { get; set; }
    }
}
