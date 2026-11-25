using DS.Platform.Application.DTOs.Customers.Response;
using DS.Platform.Domain.Entities.Customers;
using DS.Platform.Shared.Commons.Bases.Request;

namespace DS.Platform.Application.Interfaces.Repositories.Customers
{
    public interface ICustomerRepositoryAsync : IGenericRepositoryAsync<Customer, int>
    {
        /// <summary>Página del listado ya proyectada (con sus suscripciones abiertas), ordenada por razón social.</summary>
        Task<(IEnumerable<CustomerResponseDto>, int)> GetAllPaginatedAsync(BaseFiltersRequest filters, CancellationToken cancellationToken);

        /// <summary>Buscador de clientes activos por RUC o nombre (máx. <paramref name="take"/> filas).</summary>
        Task<IEnumerable<CustomerLookupResponseDto>> LookupAsync(string? text, int take, CancellationToken cancellationToken);

        Task<bool> IsUniqueRucAsync(string ruc, int? excludeId, CancellationToken cancellationToken);
    }
}
