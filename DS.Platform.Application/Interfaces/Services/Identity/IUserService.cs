using DS.Platform.Application.DTOs.Identities.Request;
using DS.Platform.Application.DTOs.Identities.Response;
using DS.Platform.Application.Interfaces.Common;
using DS.Platform.Shared.Commons.Bases.Request;
using DS.Platform.Shared.Wrapper;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DS.Platform.Application.Interfaces.Services.Identity
{
    public interface IUserService : IService
    {
        Task<Result<List<UserResponse>>> GetAllAsync(BaseFiltersRequest filters);

        Task<Result<List<UserSelectResponse>>> GetUserSelectAsync();
        Task<int> GetCountAsync();

        Task<IResult<UserByIdResponse>> GetAsync(string userId);

        Task<List<UserByIdResponse>> GetUsersByIdsAsync(List<string> userIds);
        //Task<List<UserByIdResponse>> GetUsersByRoleIdAsync(int roleId);

        Task<IResult> RegisterAsync(RegisterRequest request);

        Task<IResult> ToggleUserStatusAsync(ToggleUserStatusRequest request);

        Task<IResult<UserRolesResponse>> GetRolesAsync(string id);

        Task<IResult> UpdateRolesAsync(UpdateUserRolesRequest request);

        Task<IResult<string>> ConfirmEmailAsync(string userId, string code);

        Task<IResult> ForgotPasswordAsync(ForgotPasswordRequest request);

        Task<IResult> ResetPasswordAsync(ResetPasswordRequest request);

    }
}