using DS.Platform.Application.DTOs.Instances.Response;
using DS.Platform.Application.Extensions;
using DS.Platform.Application.Interfaces.Repositories.Erp;
using DS.Platform.Application.Mappings.Instances;
using DS.Platform.Domain.Entities.Erp;
using DS.Platform.Infrastructure.Persistence.Contexts;
using DS.Platform.Infrastructure.Persistence.Repositories.Common;
using DS.Platform.Infrastructure.Persistence.Repository;
using DS.Platform.Shared.Commons.Bases.Request;
using Microsoft.EntityFrameworkCore;

namespace DS.Platform.Infrastructure.Persistence.Repositories.Erp
{
    public class ErpInstanceRepositoryAsync : GenericRepositoryAsync<ErpInstance, int>, IErpInstanceRepositoryAsync
    {
        private readonly DbSet<ErpInstance> _instances;

        public ErpInstanceRepositoryAsync(ApplicationDbContext context) : base(context)
        {
            _instances = context.Set<ErpInstance>();
        }

        public async Task<(IEnumerable<InstanceResponseDto>, int)> GetAllPaginatedAsync(
            BaseFiltersRequest filters, int? customerId, CancellationToken cancellationToken)
        {
            var query = _instances.AsNoTracking().Where(x => !x.IsDeleted);

            if (customerId.HasValue)
            {
                query = query.Where(x => x.CustomerId == customerId.Value);
            }
            if (!string.IsNullOrEmpty(filters.TextFilter))
            {
                query = query.WhereSearch(x => x.Name.Contains(filters.TextFilter) || x.Url!.Contains(filters.TextFilter)
                                               || x.Customer!.BusinessName.Contains(filters.TextFilter)
                                               || x.Customer!.Ruc.Contains(filters.TextFilter));
            }

            var total = await query.CountAsync(cancellationToken);
            // Sin contacto primero (null = nunca se conectó), luego la que lleva más tiempo callada.
            var items = await query
                .OrderBy(x => x.LastContactOnUtc != null)
                .ThenBy(x => x.LastContactOnUtc)
                .ThenBy(x => x.Id)
                .PageOf(filters)
                .Select(InstanceMappings.ToResponse)
                .ToListAsync(cancellationToken);
            return (items, total);
        }

        public Task<ErpInstance?> GetByKeyHashAsync(string keyHash, CancellationToken cancellationToken)
            => _instances.FirstOrDefaultAsync(x => !x.IsDeleted && x.LicenseKeyHash == keyHash, cancellationToken);

        public Task<bool> UrlInUseAsync(string url, int excludeId, CancellationToken cancellationToken)
        {
            var plain = url.Trim().TrimEnd('/').ToLower();
            var withSlash = plain + "/";
            return _instances.AnyAsync(x => !x.IsDeleted && x.Id != excludeId && x.Url != null
                                            && (x.Url.ToLower() == plain || x.Url.ToLower() == withSlash), cancellationToken);
        }

        public Task<bool> NameExistsAsync(int customerId, string name, int? excludeId, CancellationToken cancellationToken)
        {
            var lower = name.Trim().ToLower();
            return _instances.AnyAsync(x => !x.IsDeleted && x.CustomerId == customerId && x.Name.ToLower() == lower
                                            && (excludeId == null || x.Id != excludeId), cancellationToken);
        }

        public Task<bool> AnyForCustomerAsync(int customerId, CancellationToken cancellationToken)
            => _instances.AnyAsync(x => !x.IsDeleted && x.CustomerId == customerId, cancellationToken);
    }

    public class LicenseIssueRepositoryAsync : GenericRepositoryAsync<LicenseIssue, int>, ILicenseIssueRepositoryAsync
    {
        private readonly DbSet<LicenseIssue> _issues;

        public LicenseIssueRepositoryAsync(ApplicationDbContext context) : base(context)
        {
            _issues = context.Set<LicenseIssue>();
        }

        public Task<LicenseIssue?> GetLastAsync(int instanceId, CancellationToken cancellationToken)
            => _issues.AsNoTracking().Where(x => !x.IsDeleted && x.InstanceId == instanceId)
                .OrderByDescending(x => x.IssuedOnUtc).ThenByDescending(x => x.Id)
                .FirstOrDefaultAsync(cancellationToken);
    }
}
