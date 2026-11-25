using DS.Platform.Application.DTOs.Portal.Response;
using DS.Platform.Application.Extensions;
using DS.Platform.Application.Interfaces.Repositories.Ops;
using DS.Platform.Application.Mappings.Portal;
using DS.Platform.Domain.Entities.Ops;
using DS.Platform.Domain.Enums;
using DS.Platform.Infrastructure.Persistence.Contexts;
using DS.Platform.Infrastructure.Persistence.Repositories.Common;
using DS.Platform.Infrastructure.Persistence.Repository;
using DS.Platform.Shared.Commons.Bases.Request;
using Microsoft.EntityFrameworkCore;

namespace DS.Platform.Infrastructure.Persistence.Repositories.Ops
{
    public class PortalAccessRepositoryAsync : GenericRepositoryAsync<PortalAccess, int>, IPortalAccessRepositoryAsync
    {
        private readonly DbSet<PortalAccess> _accesses;

        public PortalAccessRepositoryAsync(ApplicationDbContext context) : base(context)
        {
            _accesses = context.Set<PortalAccess>();
        }

        public async Task<(IEnumerable<PortalAccessResponseDto>, int)> GetAllPaginatedAsync(
            BaseFiltersRequest filters, PortalAccessFilter filter, CancellationToken cancellationToken)
        {
            var query = _accesses.AsNoTracking().Where(x => !x.IsDeleted);

            if (filter.Status.HasValue)
            {
                var status = filter.Status.Value;
                query = query.Where(x => x.Status == status);
            }
            if (filter.CustomerId.HasValue)
            {
                query = query.Where(x => x.CustomerId == filter.CustomerId.Value);
            }
            if (filter.IsEnabled.HasValue)
            {
                var enabled = filter.IsEnabled.Value;
                query = query.Where(x => x.IsEnabled == enabled);
            }
            if (!string.IsNullOrEmpty(filters.TextFilter))
            {
                query = query.WhereSearch(x => x.Email.Contains(filters.TextFilter)
                                               || x.Name.Contains(filters.TextFilter)
                                               || x.Customer!.BusinessName.Contains(filters.TextFilter)
                                               || x.Customer!.Ruc.Contains(filters.TextFilter));
            }

            var total = await query.CountAsync(cancellationToken);
            var items = await query
                .OrderByDescending(x => x.Id)
                .PageOf(filters)
                .Select(PortalAccessMappings.ToResponse)
                .ToListAsync(cancellationToken);
            return (items, total);
        }

        public Task<PortalAccess?> GetWithIssuersAsync(int id, CancellationToken cancellationToken)
            => _accesses.Include(x => x.Issuers).FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted, cancellationToken);

        public Task<PortalAccess?> GetByEmailWithIssuersAsync(string email, CancellationToken cancellationToken)
            => _accesses.Include(x => x.Issuers).FirstOrDefaultAsync(x => x.Email == email && !x.IsDeleted, cancellationToken);

        public Task<List<PortalAccess>> GetDueAsync(DateTime nowUtc, int take, CancellationToken cancellationToken)
            => _accesses.Include(x => x.Issuers)
                .Where(x => !x.IsDeleted
                            && (((x.Status == RemoteActionStatus.Pending || x.Status == RemoteActionStatus.Failed) && x.NextAttemptOnUtc <= nowUtc)
                                || (x.Status == RemoteActionStatus.Sending && x.LockedUntilUtc != null && x.LockedUntilUtc < nowUtc)))
                .OrderBy(x => x.Id)
                .Take(take)
                .ToListAsync(cancellationToken);
    }
}
