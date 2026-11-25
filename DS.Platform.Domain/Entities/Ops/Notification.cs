using DS.Platform.Domain.Contracts;
using DS.Platform.Domain.Entities.Billing;
using DS.Platform.Domain.Entities.Customers;
using DS.Platform.Domain.Enums;
using System;

namespace DS.Platform.Domain.Entities.Ops
{
    /// <summary>
    /// Aviso por correo a un cliente (diseño §5.4). El caso de uso que lo origina solo guarda esta fila (con el asunto y el cuerpo ya
    /// armados) en su mismo <c>Commit</c>; el job <c>NotificationsDispatch</c> la envía con reintentos. <see cref="DedupKey"/> es única:
    /// el mismo aviso del mismo periodo no se encola dos veces.
    /// </summary>
    public class Notification : AuditableEntity<int>, IConcurrencyChecked
    {
        public required NotificationType Type { get; set; }
        public required int CustomerId { get; set; }
        public int? SubscriptionId { get; set; }
        public int? PaymentId { get; set; }
        /// <summary>Solo avisos de certificado: la empresa emisora (en la API SUNAT).</summary>
        public long? TenantId { get; set; }

        /// <summary>Evita repetir: tipo + suscripción / empresa + el periodo (p. ej. <c>due-soon:12:20261130</c>).</summary>
        public required string DedupKey { get; set; }
        /// <summary>Correo del contacto al encolar; null si el cliente no tiene (queda Omitida).</summary>
        public string? ToEmail { get; set; }
        public required string Subject { get; set; }
        /// <summary>HTML del correo (la constancia de pago va aquí).</summary>
        public required string Body { get; set; }

        /// <summary>Cuándo se encoló (o se reintentó a mano): de aquí cuenta la caducidad de 3 días. Del reloj del caso de uso, no de la auditoría.</summary>
        public DateTime QueuedOnUtc { get; set; }

        public NotificationStatus Status { get; set; } = NotificationStatus.Pending;
        public int Attempts { get; set; }
        public DateTime NextAttemptOnUtc { get; set; }
        public DateTime? LockedUntilUtc { get; set; }
        public string? LastError { get; set; }
        public DateTime? SentOnUtc { get; set; }

        public virtual Customer? Customer { get; set; }
        public virtual Payment? Payment { get; set; }
    }
}
