using DS.Platform.Application.DTOs.Subscriptions.Response;
using DS.Platform.Domain.Entities.Billing;
using DS.Platform.Domain.Enums;
using DS.Platform.Shared.Commons.Bases.Request;

namespace DS.Platform.Application.Interfaces.Repositories.Subscriptions
{
    /// <summary>Filtros propios del listado de suscripciones (además del texto y la paginación de BaseFiltersRequest).</summary>
    public sealed record SubscriptionFilter(int? CustomerId, PlanProduct? Product, SubscriptionStatus? Status, int? PlanId);

    public interface ISubscriptionRepositoryAsync : IGenericRepositoryAsync<Subscription, int>
    {
        /// <summary>Página del listado proyectada con el cliente y el plan; ordenada por fecha de suspensión (las más urgentes primero).</summary>
        Task<(IEnumerable<SubscriptionResponseDto>, int)> GetAllPaginatedAsync(
            BaseFiltersRequest filters, SubscriptionFilter filter, CancellationToken cancellationToken);

        Task<SubscriptionResponseDto?> GetResponseByIdAsync(int id, CancellationToken cancellationToken);

        /// <summary>¿El cliente ya tiene una suscripción no cancelada de ese producto?</summary>
        Task<bool> HasOpenAsync(int customerId, PlanProduct product, int? excludeId, CancellationToken cancellationToken);

        /// <summary>¿El cliente tuvo alguna vez una suscripción de ese producto (también cancelada o borrada)? Entonces ya usó su prueba.</summary>
        Task<bool> EverSubscribedIncludingDeletedAsync(int customerId, PlanProduct product, CancellationToken cancellationToken);

        Task<bool> CustomerHasOpenAsync(int customerId, CancellationToken cancellationToken);

        Task<bool> PlanHasOpenAsync(int planId, CancellationToken cancellationToken);

        /// <summary>Clientes con una suscripción no cancelada a este plan (para reaplicar su plan de la API si el plan cambió).</summary>
        Task<List<int>> GetCustomerIdsByPlanAsync(int planId, CancellationToken cancellationToken);

        /// <summary>Suscripciones del ERP del cliente con su plan (sin seguimiento), para armar la licencia de su instancia.</summary>
        Task<List<Subscription>> GetErpWithPlanByCustomerAsync(int customerId, CancellationToken cancellationToken);

        /// <summary>Suscripciones del ERP de esos clientes (sin seguimiento, una sola consulta), para el estado de licencia de la lista de instancias.</summary>
        Task<List<Subscription>> GetErpByCustomersAsync(IReadOnlyCollection<int> customerIds, CancellationToken cancellationToken);

        /// <summary>Todas las suscripciones no borradas del cliente (también canceladas), con seguimiento, para decidir si sus emisores se suspenden.</summary>
        Task<List<Subscription>> GetByCustomerAsync(int customerId, CancellationToken cancellationToken);

        /// <summary>Suscripciones no canceladas, con seguimiento, para el recálculo diario de estados.</summary>
        Task<List<Subscription>> GetOpenForEvaluationAsync(CancellationToken cancellationToken);
    }
}
