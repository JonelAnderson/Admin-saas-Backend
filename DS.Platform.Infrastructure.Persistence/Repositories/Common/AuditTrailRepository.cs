using DS.Platform.Application.DTOs.Audits.Request;
using DS.Platform.Application.Extensions;
using DS.Platform.Application.Interfaces.Repositories.Common;
using DS.Platform.Domain.Entities.Audit;
using DS.Platform.Infrastructure.Persistence.Contexts;
using Microsoft.EntityFrameworkCore;

namespace DS.Platform.Infrastructure.Persistence.Repositories.Common
{
    public class AuditTrailRepository : IAuditTrailRepository
    {
        private readonly ApplicationDbContext _context;

        public AuditTrailRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        public Task<List<Audit>> GetLatestByUserAsync(string userId, int take, CancellationToken cancellationToken)
            => _context.AuditTrails.AsNoTracking()
                .Where(a => a.UserId == userId)
                .OrderByDescending(a => a.Id)
                .Take(take)
                .ToListAsync(cancellationToken);

        public async Task<(List<Audit> Items, int Total)> GetPaginatedAsync(AuditTrailFiltersRequest filters, DateTime? from, DateTime? toExclusive,
            CancellationToken cancellationToken)
        {
            var query = _context.AuditTrails.AsNoTracking().AsQueryable();

            if (!string.IsNullOrWhiteSpace(filters.TableName))
            {
                var table = filters.TableName.Trim();
                query = query.Where(a => a.TableName == table);
            }
            if (!string.IsNullOrWhiteSpace(filters.Type))
            {
                var type = filters.Type.Trim();
                query = query.Where(a => a.Type == type);
            }
            if (from is DateTime start)
                query = query.Where(a => a.DateTime >= start);
            if (toExclusive is DateTime end)
                query = query.Where(a => a.DateTime < end);
            if (!string.IsNullOrWhiteSpace(filters.User))
            {
                var user = filters.User.Trim();
                var userIds = _context.Users.AsNoTracking()
                    .WhereSearch(u => u.UserName!.Contains(user) || u.FirstName!.Contains(user) || u.LastName!.Contains(user))
                    .Select(u => u.Id);
                query = query.Where(a => a.UserId != null && userIds.Contains(a.UserId));
            }
            if (!string.IsNullOrWhiteSpace(filters.TextFilter))
            {
                var text = filters.TextFilter.Trim();
                query = query.WhereSearch(a => a.PrimaryKey!.Contains(text) || a.NewValues!.Contains(text) || a.OldValues!.Contains(text));
            }

            var total = await query.CountAsync(cancellationToken);
            var items = await query.ApplyPaging(filters).ToListAsync(cancellationToken);
            return (items, total);
        }

        public Task<List<string>> TableNamesAsync(CancellationToken cancellationToken)
            => _context.AuditTrails.AsNoTracking()
                .Where(a => a.TableName != null)
                .Select(a => a.TableName!)
                .Distinct()
                .OrderBy(t => t)
                .ToListAsync(cancellationToken);

        public async Task<Dictionary<string, string>> UserNamesAsync(IReadOnlyCollection<string> userIds, CancellationToken cancellationToken)
        {
            if (userIds.Count == 0)
                return [];
            var ids = userIds.ToList();
            var users = await _context.Users.AsNoTracking().Where(u => ids.Contains(u.Id))
                .Select(u => new { u.Id, u.FirstName, u.LastName, u.UserName }).ToListAsync(cancellationToken);
            return users.ToDictionary(u => u.Id, u => $"{u.FirstName} {u.LastName}".Trim() is { Length: > 0 } name ? name : u.UserName ?? u.Id);
        }
    }
}
