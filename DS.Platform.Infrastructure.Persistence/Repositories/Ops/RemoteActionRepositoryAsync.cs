using DS.Platform.Application.DTOs.RemoteActions.Response;
using DS.Platform.Application.Extensions;
using DS.Platform.Application.Interfaces.Repositories.Ops;
using DS.Platform.Application.Mappings.RemoteActions;
using DS.Platform.Domain.Entities.Ops;
using DS.Platform.Domain.Enums;
using DS.Platform.Infrastructure.Persistence.Contexts;
using DS.Platform.Infrastructure.Persistence.Repositories.Common;
using DS.Platform.Infrastructure.Persistence.Repository;
using DS.Platform.Shared.Commons.Bases.Request;
using Microsoft.EntityFrameworkCore;

namespace DS.Platform.Infrastructure.Persistence.Repositories.Ops
{
    public class RemoteActionRepositoryAsync : GenericRepositoryAsync<RemoteAction, int>, IRemoteActionRepositoryAsync
    {
        private readonly DbSet<RemoteAction> _actions;

        public RemoteActionRepositoryAsync(ApplicationDbContext context) : base(context)
        {
            _actions = context.Set<RemoteAction>();
        }

        public async Task<(IEnumerable<RemoteActionResponseDto>, int)> GetAllPaginatedAsync(
            BaseFiltersRequest filters, RemoteActionFilter filter, CancellationToken cancellationToken)
        {
            var query = _actions.AsNoTracking().Where(x => !x.IsDeleted);

            if (filter.Status.HasValue)
            {
                var status = filter.Status.Value;
                query = query.Where(x => x.Status == status);
            }
            if (filter.IssuerId.HasValue)
            {
                query = query.Where(x => x.IssuerId == filter.IssuerId.Value);
            }
            if (filter.CustomerId.HasValue)
            {
                query = query.Where(x => x.CustomerId == filter.CustomerId.Value);
            }
            if (!string.IsNullOrEmpty(filters.TextFilter))
            {
                query = query.WhereSearch(x => x.Issuer!.Ruc.Contains(filters.TextFilter)
                                               || x.Issuer!.BusinessName.Contains(filters.TextFilter)
                                               || x.Reason.Contains(filters.TextFilter));
            }

            var total = await query.CountAsync(cancellationToken);
            var items = await query
                .OrderByDescending(x => x.Id)
                .PageOf(filters)
                .Select(RemoteActionMappings.ToResponse)
                .ToListAsync(cancellationToken);
            return (items, total);
        }

        public Task<List<RemoteAction>> GetOpenForIssuerAsync(int issuerId, CancellationToken cancellationToken)
            => _actions.Where(x => !x.IsDeleted && x.IssuerId == issuerId
                                   && (x.Status == RemoteActionStatus.Pending || x.Status == RemoteActionStatus.Sending
                                       || x.Status == RemoteActionStatus.Failed || x.Status == RemoteActionStatus.Abandoned))
                .OrderBy(x => x.Id)
                .ToListAsync(cancellationToken);

        public Task<List<RemoteAction>> GetDueAsync(DateTime nowUtc, int take, CancellationToken cancellationToken)
            => _actions.Where(x => !x.IsDeleted
                                   && (((x.Status == RemoteActionStatus.Pending || x.Status == RemoteActionStatus.Failed) && x.NextAttemptOnUtc <= nowUtc)
                                       || (x.Status == RemoteActionStatus.Sending && x.LockedUntilUtc != null && x.LockedUntilUtc < nowUtc)))
                .OrderBy(x => x.Id)
                .Take(take)
                .ToListAsync(cancellationToken);

        public Task<int> CountNeedingAttentionAsync(CancellationToken cancellationToken)
            => _actions.CountAsync(x => !x.IsDeleted
                                        && (x.Status == RemoteActionStatus.Failed || x.Status == RemoteActionStatus.Abandoned),
                cancellationToken);
    }
}
