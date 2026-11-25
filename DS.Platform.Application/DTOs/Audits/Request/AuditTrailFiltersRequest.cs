using DS.Platform.Shared.Commons.Bases.Request;

namespace DS.Platform.Application.DTOs.Audits.Request
{
    /// <summary>
    /// Filtros de la pista de auditoría (solo SuperAdmin). Además de la página: <see cref="BaseFiltersRequest.StartDate"/> /
    /// <see cref="BaseFiltersRequest.EndDate"/> (yyyy-MM-dd, hora de Lima, ambos incluidos) y <see cref="BaseFiltersRequest.TextFilter"/>
    /// (busca en la clave y en los valores anteriores / nuevos).
    /// </summary>
    public class AuditTrailFiltersRequest : BaseFiltersRequest
    {
        /// <summary>Entidad exacta (p. ej. "Plan"), de GET api/Audits/Tables.</summary>
        public string? TableName { get; set; }

        /// <summary>Create, Update o Delete.</summary>
        public string? Type { get; set; }

        /// <summary>Texto del usuario: nombre de usuario, nombres o apellidos.</summary>
        public string? User { get; set; }
    }
}
