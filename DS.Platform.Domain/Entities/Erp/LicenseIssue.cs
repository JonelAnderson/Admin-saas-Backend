using DS.Platform.Domain.Contracts;
using DS.Platform.Domain.Enums;
using System;

namespace DS.Platform.Domain.Entities.Erp
{
    /// <summary>
    /// Historial de licencias entregadas a una instancia: una fila cada vez que cambia lo que se le entrega (estado, cubierto hasta
    /// o plan), no en cada consulta. Sirve para saber desde cuándo una instancia está en solo lectura.
    /// </summary>
    public class LicenseIssue : AuditableEntity<int>
    {
        public required int InstanceId { get; set; }
        public required DateTime IssuedOnUtc { get; set; }
        public required LicenseStatus Status { get; set; }
        public string? PlanCode { get; set; }
        public DateOnly? CoveredUntil { get; set; }
        public DateOnly? SuspendOn { get; set; }

        public virtual ErpInstance? Instance { get; set; }
    }
}
