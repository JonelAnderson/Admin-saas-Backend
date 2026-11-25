using DS.Platform.Application.DTOs.Portal.Response;
using DS.Platform.Domain.Entities.Ops;
using DS.Platform.Domain.Enums;
using DS.Platform.Shared.Commons.Bases.Request;

namespace DS.Platform.Application.Interfaces.Repositories.Ops
{
    public sealed record PortalAccessFilter(RemoteActionStatus? Status, int? CustomerId, bool? IsEnabled);

    public interface IPortalAccessRepositoryAsync : IGenericRepositoryAsync<PortalAccess, int>
    {
        /// <summary>Página del listado con el cliente y sus empresas, los más recientes primero.</summary>
        Task<(IEnumerable<PortalAccessResponseDto>, int)> GetAllPaginatedAsync(
            BaseFiltersRequest filters, PortalAccessFilter filter, CancellationToken cancellationToken);

        /// <summary>El acceso con sus empresas, con seguimiento (para editarlo).</summary>
        Task<PortalAccess?> GetWithIssuersAsync(int id, CancellationToken cancellationToken);

        /// <summary>El acceso de ese correo (en minúsculas) con sus empresas, con seguimiento; null si no hay.</summary>
        Task<PortalAccess?> GetByEmailWithIssuersAsync(string email, CancellationToken cancellationToken);

        /// <summary>
        /// Accesos con trabajo que toca ejecutar ahora, con seguimiento y con sus empresas: Pendientes y Fallidos cuya hora llegó, y
        /// En curso cuyo bloqueo venció. Quien los toma los marca En curso y guarda: si otro despachador los tomó antes, el guardado
        /// choca por <c>xmin</c>.
        /// </summary>
        Task<List<PortalAccess>> GetDueAsync(DateTime nowUtc, int take, CancellationToken cancellationToken);
    }
}
