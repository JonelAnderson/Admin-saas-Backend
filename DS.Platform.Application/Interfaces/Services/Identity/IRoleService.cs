using DS.Platform.Application.DTOs.Identities.Request;
using DS.Platform.Application.DTOs.Identities.Response;
using DS.Platform.Application.Interfaces.Common;
using DS.Platform.Shared.Wrapper;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DS.Platform.Application.Interfaces.Services.Identity
{
    public interface IRoleService : IService
    {
        Task<Result<IEnumerable<RoleResponse>>> GetAllAsync();

        Task<int> GetCountAsync();

        Task<Result<RoleResponse>> GetByIdAsync(string id);

        Task<Result<string>> SaveAsync(RoleRequest request);

        Task<Result<string>> DeleteAsync(string id);

        Task<Result<PermissionResponse>> GetAllPermissionsAsync(string roleId);
        Task<Result<PermissionResponse>> GetAllPermissionsByRoleNameAsync(string roleName);

        Task<Result<string>> UpdatePermissionsAsync(PermissionRequest request);

        Task<Result<List<int>>> GetMenuIdsByRoleIdAsync(string roleId);

        Task<Result<string>> UpdateRoleMenusAsync(UpdateRoleMenusRequest request);



    }
}