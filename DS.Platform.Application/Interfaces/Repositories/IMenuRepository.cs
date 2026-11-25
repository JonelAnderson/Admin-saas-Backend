using DS.Platform.Application.DTOs.Menu.Response;
using DS.Platform.Domain.Entities.Menus;
using DS.Platform.Shared.Commons.Bases.Request;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DS.Platform.Application.Interfaces.Repositories
{
    public interface IMenuRepository : IGenericRepositoryAsync<Menu, int>
    {
        Task<(IEnumerable<MenuResponseDto>, int)> GetAllMenusPaginated(BaseFiltersRequest filters);

        /// <summary>Read only, not deleted menus by order (then id).</summary>
        Task<List<Menu>> GetAllNotDeletedAsync(CancellationToken cancellationToken);
    }
    
}
