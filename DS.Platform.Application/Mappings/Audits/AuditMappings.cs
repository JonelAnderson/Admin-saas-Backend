using DS.Platform.Application.DTOs.Audits.Response;
using DS.Platform.Domain.Entities.Audit;

namespace DS.Platform.Application.Mappings.Audits
{
    /// <summary>Mapeo explícito de la pista de auditoría (sin AutoMapper).</summary>
    public static class AuditMappings
    {
        /// <summary>
        /// Fila de la pantalla de auditoría. La fecha ya es hora de Lima (UtcNow - 5 h) aunque PostgreSQL la marque UTC: sin
        /// zona, se muestra tal cual. UserName lo completa el handler (en lote).
        /// </summary>
        public static AuditTrailResponseDto ToTrailDto(this Audit a) => new()
        {
            Id = a.Id,
            UserId = a.UserId,
            IpAddress = a.IpAddress,
            Type = a.Type,
            TableName = a.TableName,
            DateTime = DateTime.SpecifyKind(a.DateTime, DateTimeKind.Unspecified),
            OldValues = a.OldValues,
            NewValues = a.NewValues,
            AffectedColumns = a.AffectedColumns,
            PrimaryKey = a.PrimaryKey
        };

        public static AuditResponse ToResponse(this Audit a) => new()
        {
            Id = a.Id,
            UserId = a.UserId,
            Type = a.Type,
            TableName = a.TableName,
            DateTime = a.DateTime,
            OldValues = a.OldValues,
            NewValues = a.NewValues,
            AffectedColumns = a.AffectedColumns,
            PrimaryKey = a.PrimaryKey
        };
    }
}
