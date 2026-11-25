using DS.Platform.Application.DTOs.Audits.Request;
using DS.Platform.Domain.Entities.Audit;

namespace DS.Platform.Application.Interfaces.Repositories.Common
{
    /// <summary>Audit trail rows written by the DbContext on every save (read only).</summary>
    public interface IAuditTrailRepository
    {
        /// <summary>The user's latest <paramref name="take"/> rows, newest first.</summary>
        Task<List<Audit>> GetLatestByUserAsync(string userId, int take, CancellationToken cancellationToken);

        /// <summary>
        /// Página de toda la pista (todas las empresas), la más reciente primero, con su total. <paramref name="from"/> /
        /// <paramref name="toExclusive"/> son hora de Lima, como se guarda la columna.
        /// </summary>
        Task<(List<Audit> Items, int Total)> GetPaginatedAsync(AuditTrailFiltersRequest filters, DateTime? from, DateTime? toExclusive,
            CancellationToken cancellationToken);

        /// <summary>Las entidades que tienen filas en la pista, en orden alfabético (para el filtro).</summary>
        Task<List<string>> TableNamesAsync(CancellationToken cancellationToken);

        /// <summary>Nombres y apellidos (o el usuario) de esos usuarios, incluidos los desactivados.</summary>
        Task<Dictionary<string, string>> UserNamesAsync(IReadOnlyCollection<string> userIds, CancellationToken cancellationToken);
    }
}
