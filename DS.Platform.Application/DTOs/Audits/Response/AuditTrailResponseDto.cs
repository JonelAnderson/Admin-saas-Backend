namespace DS.Platform.Application.DTOs.Audits.Response
{
    /// <summary>Una fila de la pista de auditoría con el nombre del usuario (vista del SuperAdmin).</summary>
    public class AuditTrailResponseDto
    {
        public int Id { get; set; }
        public string? UserId { get; set; }
        /// <summary>Nombres y apellidos (o el usuario); vacío si el cambio lo hizo el sistema.</summary>
        public string? UserName { get; set; }
        public string? IpAddress { get; set; }
        public string? Type { get; set; }
        public string? TableName { get; set; }
        /// <summary>
        /// Hora de Lima tal como se guardó (la auditoría guarda UtcNow - 5 h). Sin zona horaria en el JSON, para que el
        /// navegador la muestre tal cual y no la vuelva a convertir.
        /// </summary>
        public DateTime DateTime { get; set; }
        public string? OldValues { get; set; }
        public string? NewValues { get; set; }
        public string? AffectedColumns { get; set; }
        public string? PrimaryKey { get; set; }
    }
}
