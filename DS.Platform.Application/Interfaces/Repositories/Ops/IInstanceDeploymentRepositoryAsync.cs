using DS.Platform.Application.DTOs.Deployments.Response;
using DS.Platform.Domain.Entities.Ops;
using DS.Platform.Domain.Enums;
using DS.Platform.Shared.Commons.Bases.Request;

namespace DS.Platform.Application.Interfaces.Repositories.Ops
{
    public sealed record DeploymentFilter(RemoteActionStatus? Status, int? InstanceId, int? CustomerId);

    public interface IInstanceDeploymentRepositoryAsync : IGenericRepositoryAsync<InstanceDeployment, int>
    {
        /// <summary>Página de la bandeja de despliegues, los más recientes primero.</summary>
        Task<(IEnumerable<InstanceDeploymentResponseDto>, int)> GetAllPaginatedAsync(
            BaseFiltersRequest filters, DeploymentFilter filter, CancellationToken cancellationToken);

        /// <summary>El despliegue de la instancia que aún no terminó (Pendiente, Enviando, Fallido o Abandonado), con seguimiento.</summary>
        Task<InstanceDeployment?> GetOpenForInstanceAsync(int instanceId, CancellationToken cancellationToken);

        /// <summary>
        /// Despliegues que toca ejecutar ahora, con seguimiento: Pendientes y Fallidos cuya hora llegó, y Enviando cuyo bloqueo venció.
        /// Quien los toma los marca Enviando y guarda: si otro despachador los tomó antes, el guardado choca por <c>xmin</c>.
        /// </summary>
        Task<List<InstanceDeployment>> GetDueAsync(DateTime nowUtc, int take, CancellationToken cancellationToken);

        /// <summary>Despliegues Fallidos o Abandonados, para el tablero.</summary>
        Task<int> CountNeedingAttentionAsync(CancellationToken cancellationToken);
    }
}
