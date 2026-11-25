using DS.Platform.Application.DTOs.Instances.Response;
using DS.Platform.Domain.Entities.Erp;
using DS.Platform.Shared.Commons.Bases.Request;

namespace DS.Platform.Application.Interfaces.Repositories.Erp
{
    public interface IErpInstanceRepositoryAsync : IGenericRepositoryAsync<ErpInstance, int>
    {
        /// <summary>Página del listado con el cliente; las que llevan más tiempo sin contacto primero. Sin estado de licencia (lo agrega el handler).</summary>
        Task<(IEnumerable<InstanceResponseDto>, int)> GetAllPaginatedAsync(
            BaseFiltersRequest filters, int? customerId, CancellationToken cancellationToken);

        /// <summary>La instancia de esa clave (hash), con seguimiento, para registrar su contacto.</summary>
        Task<ErpInstance?> GetByKeyHashAsync(string keyHash, CancellationToken cancellationToken);

        Task<bool> NameExistsAsync(int customerId, string name, int? excludeId, CancellationToken cancellationToken);

        Task<bool> AnyForCustomerAsync(int customerId, CancellationToken cancellationToken);

        /// <summary>Otra instancia (distinta de <paramref name="excludeId"/>) ya usa esa dirección (sin distinguir mayúsculas ni la barra final).</summary>
        Task<bool> UrlInUseAsync(string url, int excludeId, CancellationToken cancellationToken);
    }

    public interface ILicenseIssueRepositoryAsync : IGenericRepositoryAsync<LicenseIssue, int>
    {
        /// <summary>La última licencia entregada a la instancia (sin seguimiento).</summary>
        Task<LicenseIssue?> GetLastAsync(int instanceId, CancellationToken cancellationToken);
    }
}
