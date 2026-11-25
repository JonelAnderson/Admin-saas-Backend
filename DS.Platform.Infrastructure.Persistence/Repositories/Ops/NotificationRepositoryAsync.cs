using DS.Platform.Application.DTOs.Notifications.Response;
using DS.Platform.Application.Extensions;
using DS.Platform.Application.Interfaces.Repositories.Ops;
using DS.Platform.Application.Mappings.Notifications;
using DS.Platform.Domain.Entities.Ops;
using DS.Platform.Domain.Enums;
using DS.Platform.Infrastructure.Persistence.Contexts;
using DS.Platform.Infrastructure.Persistence.Repositories.Common;
using DS.Platform.Infrastructure.Persistence.Repository;
using DS.Platform.Shared.Commons.Bases.Request;
using Microsoft.EntityFrameworkCore;

namespace DS.Platform.Infrastructure.Persistence.Repositories.Ops
{
    public class NotificationRepositoryAsync : GenericRepositoryAsync<Notification, int>, INotificationRepositoryAsync
    {
        private readonly DbSet<Notification> _notifications;

        public NotificationRepositoryAsync(ApplicationDbContext context) : base(context)
        {
            _notifications = context.Set<Notification>();
        }

        public async Task<(IEnumerable<NotificationResponseDto>, int)> GetAllPaginatedAsync(
            BaseFiltersRequest filters, NotificationFilter filter, CancellationToken cancellationToken)
        {
            var query = _notifications.AsNoTracking().Where(x => !x.IsDeleted);

            if (filter.Status.HasValue)
            {
                var status = filter.Status.Value;
                query = query.Where(x => x.Status == status);
            }
            if (filter.Type.HasValue)
            {
                var type = filter.Type.Value;
                query = query.Where(x => x.Type == type);
            }
            if (filter.CustomerId.HasValue)
            {
                query = query.Where(x => x.CustomerId == filter.CustomerId.Value);
            }
            if (!string.IsNullOrEmpty(filters.TextFilter))
            {
                query = query.WhereSearch(x => x.Customer!.BusinessName.Contains(filters.TextFilter)
                                               || x.Customer!.Ruc.Contains(filters.TextFilter)
                                               || x.Subject.Contains(filters.TextFilter)
                                               || (x.ToEmail != null && x.ToEmail.Contains(filters.TextFilter)));
            }

            var total = await query.CountAsync(cancellationToken);
            var items = await query
                .OrderByDescending(x => x.Id)
                .PageOf(filters)
                .Select(NotificationMappings.ToResponse)
                .ToListAsync(cancellationToken);
            return (items, total);
        }

        public async Task<HashSet<string>> GetExistingKeysAsync(IReadOnlyCollection<string> keys, CancellationToken cancellationToken)
        {
            if (keys.Count == 0)
            {
                return new HashSet<string>();
            }
            var list = keys.Distinct().ToList();
            // La clave es única incluso entre los borrados (índice sin filtro).
            var found = await _notifications.AsNoTracking().IgnoreQueryFilters().Where(x => list.Contains(x.DedupKey))
                .Select(x => x.DedupKey).ToListAsync(cancellationToken);
            return found.ToHashSet();
        }

        public Task<List<Notification>> GetDueAsync(DateTime nowUtc, int take, CancellationToken cancellationToken)
            => _notifications.Where(x => !x.IsDeleted
                                         && (((x.Status == NotificationStatus.Pending || x.Status == NotificationStatus.Failed) && x.NextAttemptOnUtc <= nowUtc)
                                             || (x.Status == NotificationStatus.Sending && x.LockedUntilUtc != null && x.LockedUntilUtc < nowUtc)))
                .OrderBy(x => x.Id)
                .Take(take)
                .ToListAsync(cancellationToken);

        public Task<int> CountNeedingAttentionAsync(CancellationToken cancellationToken)
            => _notifications.CountAsync(x => !x.IsDeleted && (x.Status == NotificationStatus.Failed || x.Status == NotificationStatus.Abandoned),
                cancellationToken);
    }
}
