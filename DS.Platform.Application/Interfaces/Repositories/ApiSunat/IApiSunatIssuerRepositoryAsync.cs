using DS.Platform.Application.DTOs.ApiSunat.Response;
using DS.Platform.Domain.Entities.ApiSunat;
using DS.Platform.Shared.Commons.Bases.Request;

namespace DS.Platform.Application.Interfaces.Repositories.ApiSunat
{
    /// <summary>Filtros propios del listado de emisores (además del texto y la paginación de BaseFiltersRequest).</summary>
    public sealed record IssuerFilter(int? CustomerId, bool? SuspendDesired);

    public interface IApiSunatIssuerRepositoryAsync : IGenericRepositoryAsync<ApiSunatIssuer, int>
    {
        /// <summary>Página del listado proyectada con el cliente; los suspendidos primero, luego por razón social.</summary>
        Task<(IEnumerable<IssuerResponseDto>, int)> GetAllPaginatedAsync(
            BaseFiltersRequest filters, IssuerFilter filter, CancellationToken cancellationToken);

        Task<IssuerResponseDto?> GetResponseByIdAsync(int id, CancellationToken cancellationToken);

        /// <summary>Emisores del cliente (con seguimiento, para recalcular su suspensión).</summary>
        Task<List<ApiSunatIssuer>> GetByCustomerAsync(int customerId, CancellationToken cancellationToken);

        /// <summary>¿La empresa de la API o su RUC ya están vinculados a algún cliente?</summary>
        Task<bool> TenantOrRucLinkedAsync(long tenantId, string ruc, int? excludeId, CancellationToken cancellationToken);

        /// <summary>Emisores vinculados a esas empresas de la API (sin seguimiento).</summary>
        Task<List<ApiSunatIssuer>> GetByTenantIdsAsync(IReadOnlyCollection<long> tenantIds, CancellationToken cancellationToken);

        /// <summary>De esos ids de empresa de la API, los que ya están vinculados (una sola consulta).</summary>
        Task<HashSet<long>> GetLinkedTenantIdsAsync(IReadOnlyCollection<long> tenantIds, CancellationToken cancellationToken);
    }
}
