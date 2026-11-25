using DS.Platform.Application.DTOs.Customers.Response;
using DS.Platform.Application.Extensions;
using DS.Platform.Application.Interfaces.Repositories.Customers;
using DS.Platform.Application.Mappings.Customers;
using DS.Platform.Domain.Entities.Customers;
using DS.Platform.Infrastructure.Persistence.Contexts;
using DS.Platform.Infrastructure.Persistence.Repositories.Common;
using DS.Platform.Infrastructure.Persistence.Repository;
using DS.Platform.Shared.Commons.Bases.Request;
using Microsoft.EntityFrameworkCore;

namespace DS.Platform.Infrastructure.Persistence.Repositories.Customers
{
    public class CustomerRepositoryAsync : GenericRepositoryAsync<Customer, int>, ICustomerRepositoryAsync
    {
        private readonly DbSet<Customer> _customers;

        public CustomerRepositoryAsync(ApplicationDbContext context) : base(context)
        {
            _customers = context.Set<Customer>();
        }

        public async Task<(IEnumerable<CustomerResponseDto>, int)> GetAllPaginatedAsync(BaseFiltersRequest filters, CancellationToken cancellationToken)
        {
            var query = _customers.AsNoTracking().Where(x => !x.IsDeleted);

            if (!string.IsNullOrEmpty(filters.TextFilter))
            {
                query = query.WhereSearch(x => x.Ruc.Contains(filters.TextFilter) || x.BusinessName.Contains(filters.TextFilter)
                                               || x.TradeName!.Contains(filters.TextFilter));
            }

            if (filters.StateFilter is not null)
            {
                query = filters.StateFilter switch
                {
                    0 => query.Where(x => !x.IsActive),
                    1 => query.Where(x => x.IsActive),
                    _ => query
                };
            }

            var total = await query.CountAsync(cancellationToken);
            var items = await query.OrderBy(x => x.BusinessName).ThenBy(x => x.Id)
                .PageOf(filters)
                .Select(CustomerMappings.ToResponse)
                .ToListAsync(cancellationToken);
            return (items, total);
        }

        public async Task<IEnumerable<CustomerLookupResponseDto>> LookupAsync(string? text, int take, CancellationToken cancellationToken)
        {
            var query = _customers.AsNoTracking().Where(x => !x.IsDeleted && x.IsActive);
            if (!string.IsNullOrWhiteSpace(text))
            {
                var value = text.Trim();
                query = query.WhereSearch(x => x.Ruc.Contains(value) || x.BusinessName.Contains(value) || x.TradeName!.Contains(value));
            }

            return await query.OrderBy(x => x.BusinessName).ThenBy(x => x.Id).Take(take)
                .Select(CustomerMappings.ToLookup)
                .ToListAsync(cancellationToken);
        }

        public async Task<bool> IsUniqueRucAsync(string ruc, int? excludeId, CancellationToken cancellationToken)
        {
            var value = ruc.Trim();
            var query = _customers.Where(x => !x.IsDeleted && x.Ruc == value);
            if (excludeId.HasValue)
            {
                query = query.Where(x => x.Id != excludeId.Value);
            }
            return !await query.AnyAsync(cancellationToken);
        }
    }
}
