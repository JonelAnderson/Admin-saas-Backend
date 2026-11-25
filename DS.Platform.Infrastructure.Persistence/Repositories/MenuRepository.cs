using DS.Platform.Application.Mappings.Menus;
using DS.Platform.Application.DTOs.Menu.Response;
using DS.Platform.Application.Extensions;
using DS.Platform.Application.Interfaces.Repositories;
using DS.Platform.Domain.Entities.Menus;
using DS.Platform.Infrastructure.Persistence.Contexts;
using DS.Platform.Infrastructure.Persistence.Repository;
using DS.Platform.Shared.Commons.Bases.Request;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DS.Platform.Infrastructure.Persistence.Repositories.Common;

namespace DS.Platform.Infrastructure.Persistence.Repositories
{
    public class MenuRepository : GenericRepositoryAsync<Menu, int>, IMenuRepository
    {
        private readonly ApplicationDbContext _context;
        private readonly DbSet<Menu> _menus;

        public MenuRepository(ApplicationDbContext context) : base(context)
        {
            _context = context;
            _menus = _context.Set<Menu>();
        }

        public async Task<(IEnumerable<MenuResponseDto>, int)> GetAllMenusPaginated(BaseFiltersRequest filters)
        {
            var query = _menus.AsNoTracking().AsQueryable();

            // Filtros
            query = query.Where(x => !x.IsDeleted);

            if (!string.IsNullOrEmpty(filters.TextFilter))
            {
                query = query.WhereSearch(x => x.Title!.Contains(filters.TextFilter) || x.Type.Contains(filters.TextFilter));
            }

            if (filters.StateFilter is not null)
            {
                if (filters.StateFilter == 0) query = query.Where(x => !x.IsActive);
                else if (filters.StateFilter == 1) query = query.Where(x => x.IsActive);
            }

            var totalRecords = await query.CountAsync();

            // Order first, then the page (offset or LastId cursor): see QueryableExtensions.ApplyPaging.
            query = query.ApplyPaging(filters);

            var menus = await query.Select(MenuMappings.ToResponse).ToListAsync();

            return (menus, totalRecords);
        }

        public Task<List<Menu>> GetAllNotDeletedAsync(CancellationToken cancellationToken)
            => _menus.AsNoTracking().Where(x => !x.IsDeleted).OrderBy(x => x.Order).ThenBy(x => x.Id).ToListAsync(cancellationToken);

    }
}
