using DS.Platform.Application.DTOs.Deployments.Response;
using DS.Platform.Application.Extensions;
using DS.Platform.Application.Interfaces.Repositories.Ops;
using DS.Platform.Application.Mappings.Deployments;
using DS.Platform.Domain.Entities.Ops;
using DS.Platform.Domain.Enums;
using DS.Platform.Infrastructure.Persistence.Contexts;
using DS.Platform.Infrastructure.Persistence.Repositories.Common;
using DS.Platform.Infrastructure.Persistence.Repository;
using DS.Platform.Shared.Commons.Bases.Request;
using Microsoft.EntityFrameworkCore;

namespace DS.Platform.Infrastructure.Persistence.Repositories.Ops
{
    public class InstanceDeploymentRepositoryAsync : GenericRepositoryAsync<InstanceDeployment, int>, IInstanceDeploymentRepositoryAsync
    {
        private readonly DbSet<InstanceDeployment> _deployments;

        public InstanceDeploymentRepositoryAsync(ApplicationDbContext context) : base(context)
        {
            _deployments = context.Set<InstanceDeployment>();
        }

        public async Task<(IEnumerable<InstanceDeploymentResponseDto>, int)> GetAllPaginatedAsync(
            BaseFiltersRequest filters, DeploymentFilter filter, CancellationToken cancellationToken)
        {
            var query = _deployments.AsNoTracking().Where(x => !x.IsDeleted);

            if (filter.Status.HasValue)
            {
                var status = filter.Status.Value;
                query = query.Where(x => x.Status == status);
            }
            if (filter.InstanceId.HasValue)
            {
                query = query.Where(x => x.InstanceId == filter.InstanceId.Value);
            }
            if (filter.CustomerId.HasValue)
            {
                query = query.Where(x => x.CustomerId == filter.CustomerId.Value);
            }
            if (!string.IsNullOrEmpty(filters.TextFilter))
            {
                query = query.WhereSearch(x => x.Instance!.Name.Contains(filters.TextFilter)
                                               || x.Instance!.Customer!.BusinessName.Contains(filters.TextFilter)
                                               || x.Instance!.Customer!.Ruc.Contains(filters.TextFilter)
                                               || x.Version.Contains(filters.TextFilter));
            }

            var total = await query.CountAsync(cancellationToken);
            var items = await query
                .OrderByDescending(x => x.Id)
                .PageOf(filters)
                .Select(DeploymentMappings.ToResponse)
                .ToListAsync(cancellationToken);
            return (items, total);
        }

        public Task<InstanceDeployment?> GetOpenForInstanceAsync(int instanceId, CancellationToken cancellationToken)
            => _deployments.Where(x => !x.IsDeleted && x.InstanceId == instanceId
                                       && (x.Status == RemoteActionStatus.Pending || x.Status == RemoteActionStatus.Sending
                                           || x.Status == RemoteActionStatus.Failed || x.Status == RemoteActionStatus.Abandoned))
                .FirstOrDefaultAsync(cancellationToken);

        public Task<List<InstanceDeployment>> GetDueAsync(DateTime nowUtc, int take, CancellationToken cancellationToken)
            => _deployments.Where(x => !x.IsDeleted
                                       && (((x.Status == RemoteActionStatus.Pending || x.Status == RemoteActionStatus.Failed) && x.NextAttemptOnUtc <= nowUtc)
                                           || (x.Status == RemoteActionStatus.Sending && x.LockedUntilUtc != null && x.LockedUntilUtc < nowUtc)))
                .OrderBy(x => x.Id)
                .Take(take)
                .ToListAsync(cancellationToken);

        public Task<int> CountNeedingAttentionAsync(CancellationToken cancellationToken)
            => _deployments.CountAsync(x => !x.IsDeleted
                                            && (x.Status == RemoteActionStatus.Failed || x.Status == RemoteActionStatus.Abandoned),
                cancellationToken);
    }
}
