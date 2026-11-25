using DS.Platform.Application.DTOs.Credentials.Response;
using DS.Platform.Domain.Entities.Ops;
using DS.Platform.Domain.Enums;
using DS.Platform.Shared.Commons.Bases.Request;

namespace DS.Platform.Application.Interfaces.Repositories.Ops
{
    public sealed record ApiCredentialDeliveryFilter(ApiCredentialDeliveryStatus? Status, int? CustomerId, int? InstanceId);

    public interface IApiCredentialDeliveryRepositoryAsync : IGenericRepositoryAsync<ApiCredentialDelivery, int>
    {
        /// <summary>Página del listado con cliente, instancia y emisor, las más recientes primero. Sin secretos.</summary>
        Task<(IEnumerable<ApiCredentialDeliveryResponseDto>, int)> GetAllPaginatedAsync(
            BaseFiltersRequest filters, ApiCredentialDeliveryFilter filter, CancellationToken cancellationToken);

        /// <summary>Hay una entrega abierta (Pendiente, En curso, Lista o Fallida) de ese emisor.</summary>
        Task<bool> HasOpenForIssuerAsync(int issuerId, CancellationToken cancellationToken);

        /// <summary>
        /// Entregas con trabajo que toca ejecutar ahora, con seguimiento: Pendientes y Fallidas cuya hora llegó, y En curso cuyo
        /// bloqueo venció. Quien las toma las marca En curso y guarda: si otro despachador las tomó antes, el guardado choca por <c>xmin</c>.
        /// </summary>
        Task<List<ApiCredentialDelivery>> GetDueAsync(DateTime nowUtc, int take, CancellationToken cancellationToken);

        /// <summary>Listas cuyo plazo venció (con seguimiento): hay que borrarles el secreto.</summary>
        Task<List<ApiCredentialDelivery>> GetExpiredReadyAsync(DateTime nowUtc, int take, CancellationToken cancellationToken);

        /// <summary>Las Listas de esa instancia que aún esperan (no vencidas), con seguimiento, las más antiguas primero.</summary>
        Task<List<ApiCredentialDelivery>> GetReadyByInstanceAsync(int instanceId, DateTime nowUtc, int take, CancellationToken cancellationToken);

        /// <summary>Esas entregas, solo si son de esa instancia (con seguimiento). Un id de otra instancia no aparece.</summary>
        Task<List<ApiCredentialDelivery>> GetByIdsForInstanceAsync(int instanceId, IReadOnlyCollection<int> ids, CancellationToken cancellationToken);
    }
}
