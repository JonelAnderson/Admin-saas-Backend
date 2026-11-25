using DS.Platform.Application.DTOs.Identities.Request;
using DS.Platform.Application.DTOs.Identities.Response;
using DS.Platform.Application.Interfaces.Common;
using DS.Platform.Shared.Wrapper;
using Microsoft.AspNetCore.Http;
using System.Threading.Tasks;

namespace DS.Platform.Application.Interfaces.Services.Account
{
    public interface IAccountService : IService
    {
        Task<IResult> UpdateProfileAsync(UpdateProfileRequest model);

        Task<IResult> ChangePasswordAsync(ChangePasswordRequest model);

        Task<IResult<string>> GetProfilePictureAsync(string userId);

        /// <summary>Nombre, usuario, correo y foto del usuario conectado (no van en el JWT).</summary>
        Task<IResult<CurrentUserProfileResponse>> GetCurrentProfileAsync();

        Task<IResult<string>> UpdateProfilePictureAsync(IFormFile request, string userId);
    }
}