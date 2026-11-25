using DS.Platform.Application.DTOs.Plans.Response;
using DS.Platform.Application.Extensions;
using DS.Platform.Application.Interfaces.Repositories.Plans;
using DS.Platform.Application.Mappings.Plans;
using DS.Platform.Domain.Entities.Billing;
using DS.Platform.Domain.Enums;
using DS.Platform.Infrastructure.Persistence.Contexts;
using DS.Platform.Infrastructure.Persistence.Repositories.Common;
using DS.Platform.Infrastructure.Persistence.Repository;
using DS.Platform.Shared.Commons.Bases.Request;
using Microsoft.EntityFrameworkCore;

namespace DS.Platform.Infrastructure.Persistence.Repositories.Plans
{
    public class PlanRepositoryAsync : GenericRepositoryAsync<Plan, int>, IPlanRepositoryAsync
    {
        private readonly DbSet<Plan> _plans;

        public PlanRepositoryAsync(ApplicationDbContext context) : base(context)
        {
            _plans = context.Set<Plan>();
        }

        public async Task<(IEnumerable<PlanResponseDto>, int)> GetAllPaginatedAsync(
            BaseFiltersRequest filters, PlanProduct? product, CancellationToken cancellationToken)
        {
            var query = _plans.AsNoTracking().Where(x => !x.IsDeleted);

            if (product.HasValue)
            {
                query = query.Where(x => x.Product == product.Value);
            }

            if (!string.IsNullOrEmpty(filters.TextFilter))
            {
                query = query.WhereSearch(x => x.Name.Contains(filters.TextFilter) || x.Code.Contains(filters.TextFilter));
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

            var totalRecords = await query.CountAsync(cancellationToken);
            var items = await query
                .OrderBy(x => x.Product).ThenBy(x => x.SortOrder).ThenBy(x => x.Name).ThenBy(x => x.Id)
                .PageOf(filters)
                .Select(PlanMappings.ToResponse)
                .ToListAsync(cancellationToken);
            return (items, totalRecords);
        }

        public async Task<IEnumerable<PlanSelectResponseDto>> GetSelectAsync(PlanProduct? product, CancellationToken cancellationToken)
        {
            var query = _plans.AsNoTracking().Where(x => !x.IsDeleted && x.IsActive);
            if (product.HasValue)
            {
                query = query.Where(x => x.Product == product.Value);
            }

            return await query.OrderBy(x => x.Product).ThenBy(x => x.SortOrder).ThenBy(x => x.Name)
                .Select(PlanMappings.ToSelect)
                .ToListAsync(cancellationToken);
        }

        public async Task<bool> IsUniqueCodeAsync(PlanProduct product, string code, int? excludeId, CancellationToken cancellationToken)
        {
            var normalized = PlanMappings.NormalizeCode(code);
            var query = _plans.Where(x => !x.IsDeleted && x.Product == product && x.Code == normalized);
            if (excludeId.HasValue)
            {
                query = query.Where(x => x.Id != excludeId.Value);
            }

            return !await query.AnyAsync(cancellationToken);
        }
    }
}
