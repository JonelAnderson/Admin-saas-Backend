using DS.Platform.Application.DTOs.Notifications.Response;
using DS.Platform.Domain.Entities.Ops;
using DS.Platform.Domain.Enums;
using DS.Platform.Shared.Commons.Bases.Request;

namespace DS.Platform.Application.Interfaces.Repositories.Ops
{
    public sealed record NotificationFilter(NotificationStatus? Status, NotificationType? Type, int? CustomerId);

    public interface INotificationRepositoryAsync : IGenericRepositoryAsync<Notification, int>
    {
        /// <summary>Página de la bandeja, los más recientes primero.</summary>
        Task<(IEnumerable<NotificationResponseDto>, int)> GetAllPaginatedAsync(
            BaseFiltersRequest filters, NotificationFilter filter, CancellationToken cancellationToken);

        /// <summary>Cuáles de estas claves de aviso ya existen (borrados incluidos): para no encolar dos veces el mismo aviso.</summary>
        Task<HashSet<string>> GetExistingKeysAsync(IReadOnlyCollection<string> keys, CancellationToken cancellationToken);

        /// <summary>
        /// Avisos que toca enviar ahora, con seguimiento: Pendientes y Fallidos cuya hora llegó, y Enviando con el bloqueo vencido.
        /// Quien los toma los marca Enviando y guarda: si otro despachador los tomó antes, el guardado choca por <c>xmin</c>.
        /// </summary>
        Task<List<Notification>> GetDueAsync(DateTime nowUtc, int take, CancellationToken cancellationToken);

        /// <summary>Fallidos y abandonados (piden atención), para el tablero.</summary>
        Task<int> CountNeedingAttentionAsync(CancellationToken cancellationToken);
    }
}
