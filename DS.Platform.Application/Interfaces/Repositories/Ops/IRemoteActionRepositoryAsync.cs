using DS.Platform.Application.DTOs.RemoteActions.Response;
using DS.Platform.Domain.Entities.Ops;
using DS.Platform.Domain.Enums;
using DS.Platform.Shared.Commons.Bases.Request;

namespace DS.Platform.Application.Interfaces.Repositories.Ops
{
    public sealed record RemoteActionFilter(RemoteActionStatus? Status, int? IssuerId, int? CustomerId);

    public interface IRemoteActionRepositoryAsync : IGenericRepositoryAsync<RemoteAction, int>
    {
        /// <summary>Página de la bandeja, las más recientes primero.</summary>
        Task<(IEnumerable<RemoteActionResponseDto>, int)> GetAllPaginatedAsync(
            BaseFiltersRequest filters, RemoteActionFilter filter, CancellationToken cancellationToken);

        /// <summary>Acciones del emisor que aún no terminaron (Pendiente, Enviando, Fallida o Abandonada), con seguimiento.</summary>
        Task<List<RemoteAction>> GetOpenForIssuerAsync(int issuerId, CancellationToken cancellationToken);

        /// <summary>
        /// Acciones que toca enviar ahora, con seguimiento: Pendientes y Fallidas cuya hora llegó, y Enviando cuyo bloqueo venció
        /// (el proceso que las tomó murió). Las más antiguas primero. Quien las toma las marca Enviando y guarda: si otro
        /// despachador las tomó antes, el guardado choca por <c>xmin</c> (<c>IConcurrencyChecked</c>) y no se envían dos veces.
        /// </summary>
        Task<List<RemoteAction>> GetDueAsync(DateTime nowUtc, int take, CancellationToken cancellationToken);

        /// <summary>Cuántas acciones hay en cada estado que pide atención (Fallida / Abandonada), para el tablero.</summary>
        Task<int> CountNeedingAttentionAsync(CancellationToken cancellationToken);
    }
}
