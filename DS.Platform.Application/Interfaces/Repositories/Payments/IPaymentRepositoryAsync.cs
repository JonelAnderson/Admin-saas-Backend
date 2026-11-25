using DS.Platform.Application.DTOs.Payments.Response;
using DS.Platform.Domain.Entities.Billing;
using DS.Platform.Domain.Enums;
using DS.Platform.Shared.Commons.Bases.Request;

namespace DS.Platform.Application.Interfaces.Repositories.Payments
{
    /// <summary>Filtros propios del listado de pagos (fechas de pago de Lima, inclusivas).</summary>
    public sealed record PaymentFilter(int? CustomerId, int? SubscriptionId, PaymentStatus? Status, PaymentMethod? Method,
        DateOnly? From, DateOnly? To);

    public interface IPaymentRepositoryAsync : IGenericRepositoryAsync<Payment, int>
    {
        /// <summary>Página del listado proyectada (cliente, plan), del más reciente al más antiguo.</summary>
        Task<(IEnumerable<PaymentResponseDto>, int)> GetAllPaginatedAsync(BaseFiltersRequest filters, PaymentFilter filter, CancellationToken cancellationToken);

        Task<PaymentResponseDto?> GetResponseByIdAsync(int id, CancellationToken cancellationToken);

        /// <summary>¿Ya hay un pago registrado (no anulado) con ese medio y número de operación?</summary>
        Task<bool> IsOperationRegisteredAsync(PaymentMethod method, string operationNumber, CancellationToken cancellationToken);

        /// <summary>Id del último pago registrado (no anulado) de la suscripción, o null.</summary>
        Task<int?> LastRegisteredIdAsync(int subscriptionId, CancellationToken cancellationToken);
    }
}
