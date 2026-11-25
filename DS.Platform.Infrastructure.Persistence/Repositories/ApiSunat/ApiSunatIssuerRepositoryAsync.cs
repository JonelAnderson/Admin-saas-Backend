using DS.Platform.Application.DTOs.ApiSunat.Response;
using DS.Platform.Application.Extensions;
using DS.Platform.Application.Interfaces.Repositories.ApiSunat;
using DS.Platform.Application.Mappings.ApiSunat;
using DS.Platform.Domain.Entities.ApiSunat;
using DS.Platform.Infrastructure.Persistence.Contexts;
using DS.Platform.Infrastructure.Persistence.Repositories.Common;
using DS.Platform.Infrastructure.Persistence.Repository;
using DS.Platform.Shared.Commons.Bases.Request;
using Microsoft.EntityFrameworkCore;

namespace DS.Platform.Infrastructure.Persistence.Repositories.ApiSunat
{
    public class ApiSunatIssuerRepositoryAsync : GenericRepositoryAsync<ApiSunatIssuer, int>, IApiSunatIssuerRepositoryAsync
    {
        private readonly DbSet<ApiSunatIssuer> _issuers;

        public ApiSunatIssuerRepositoryAsync(ApplicationDbContext context) : base(context)
        {
            _issuers = context.Set<ApiSunatIssuer>();
        }

        public async Task<(IEnumerable<IssuerResponseDto>, int)> GetAllPaginatedAsync(
            BaseFiltersRequest filters, IssuerFilter filter, CancellationToken cancellationToken)
        {
            var query = _issuers.AsNoTracking().Where(x => !x.IsDeleted);

            if (filter.CustomerId.HasValue)
            {
                query = query.Where(x => x.CustomerId == filter.CustomerId.Value);
            }
            if (filter.SuspendDesired.HasValue)
            {
                var suspended = filter.SuspendDesired.Value;
                query = query.Where(x => x.SuspendDesired == suspended);
            }
            if (!string.IsNullOrEmpty(filters.TextFilter))
            {
                query = query.WhereSearch(x => x.Ruc.Contains(filters.TextFilter) || x.BusinessName.Contains(filters.TextFilter)
                                               || x.Customer!.BusinessName.Contains(filters.TextFilter)
                                               || x.Customer!.Ruc.Contains(filters.TextFilter));
            }

            var total = await query.CountAsync(cancellationToken);
            // Los suspendidos primero (piden atención), luego por razón social.
            var items = await query
                .OrderByDescending(x => x.SuspendDesired)
                .ThenBy(x => x.BusinessName)
                .ThenBy(x => x.Id)
                .PageOf(filters)
                .Select(IssuerMappings.ToResponse)
                .ToListAsync(cancellationToken);
            return (items, total);
        }

        public Task<IssuerResponseDto?> GetResponseByIdAsync(int id, CancellationToken cancellationToken)
            => _issuers.AsNoTracking().Where(x => !x.IsDeleted && x.Id == id)
                .Select(IssuerMappings.ToResponse)
                .FirstOrDefaultAsync(cancellationToken);

        public Task<List<ApiSunatIssuer>> GetByCustomerAsync(int customerId, CancellationToken cancellationToken)
            => _issuers.Where(x => !x.IsDeleted && x.CustomerId == customerId).OrderBy(x => x.Id).ToListAsync(cancellationToken);

        public Task<bool> TenantOrRucLinkedAsync(long tenantId, string ruc, int? excludeId, CancellationToken cancellationToken)
            => _issuers.AnyAsync(x => !x.IsDeleted && (x.TenantId == tenantId || x.Ruc == ruc) && (excludeId == null || x.Id != excludeId),
                cancellationToken);

        public Task<List<ApiSunatIssuer>> GetByTenantIdsAsync(IReadOnlyCollection<long> tenantIds, CancellationToken cancellationToken)
        {
            var ids = tenantIds.ToList();
            return _issuers.AsNoTracking().Where(x => !x.IsDeleted && ids.Contains(x.TenantId)).ToListAsync(cancellationToken);
        }

        public async Task<HashSet<long>> GetLinkedTenantIdsAsync(IReadOnlyCollection<long> tenantIds, CancellationToken cancellationToken)
        {
            var ids = tenantIds.ToList();
            var linked = await _issuers.AsNoTracking().Where(x => !x.IsDeleted && ids.Contains(x.TenantId))
                .Select(x => x.TenantId).ToListAsync(cancellationToken);
            return linked.ToHashSet();
        }
    }
}
