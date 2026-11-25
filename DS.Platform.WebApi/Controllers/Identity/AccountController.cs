using DS.Platform.Application.DTOs.Identities.Request;
using DS.Platform.Application.Interfaces.Services;
using DS.Platform.Application.Interfaces.Services.Account;
using DS.Platform.Application.Interfaces.Services.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;

namespace DS.Platform.WebApi.Controllers.Identity
{
    [Authorize]
    [Route("api/identity/account")]
    [ApiController]
    public class AccountController : ControllerBase
    {
        private readonly IAccountService _accountService;
        private readonly ICurrentUserService _currentUser;
        private readonly ITokenService _tokenService;

        public AccountController(IAccountService accountService, ICurrentUserService currentUser, ITokenService tokenService)
        {
            _accountService = accountService;
            _currentUser = currentUser;
            _tokenService = tokenService;
        }


        /// <summary>
        /// Get User Permissions
        /// </summary>
        /// <returns>List of permissions</returns>
        [HttpGet("permissions")]
        public async Task<IActionResult> GetPermissionsAsync()
        {
            var response = await _tokenService.GetPermissionsAsync(_currentUser.UserId);
            return Ok(response);
        }

        /// <summary>Nombre, usuario, correo y foto del usuario conectado (el JWT no los lleva).</summary>
        [HttpGet("me")]
        public async Task<IActionResult> GetMeAsync()
        {
            return Ok(await _accountService.GetCurrentProfileAsync());
        }

        /// <summary>
        /// Update Profile
        /// </summary>
        /// <param name="model"></param>
        /// <returns>Status 200 OK</returns>
        // La usa la administración de usuarios para editar a otro usuario: exige Users.Edit y el servicio valida que el
        // usuario y la sucursal nueva sean de la empresa del administrador (UserAdminScope).
        [Authorize(Policy = DS.Platform.Shared.Constants.Permission.Permissions.Users.Edit)]
        [HttpPut(nameof(UpdateProfile))]
        public async Task<ActionResult> UpdateProfile([FromBody] UpdateProfileRequest model)
        {
            var response = await _accountService.UpdateProfileAsync(model);
            return Ok(response);
        }

        /// <summary>
        /// Change Password
        /// </summary>
        /// <param name="model"></param>
        /// <returns>Status 200 OK</returns>
        [HttpPut(nameof(ChangePassword))]
        public async Task<ActionResult> ChangePassword(ChangePasswordRequest model)
        {
            var response = await _accountService.ChangePasswordAsync(model);
            return Ok(response);
        }

        /// <summary>
        /// Get Profile picture by Id
        /// </summary>
        /// <param name="userId"></param>
        /// <returns>Status 200 OK </returns>
        [HttpGet("profile-picture/{userId}")]
        [ResponseCache(NoStore = false, Location = ResponseCacheLocation.Client, Duration = 60)]
        public async Task<IActionResult> GetProfilePictureAsync(string userId)
        {
            return Ok(await _accountService.GetProfilePictureAsync(userId));
        }

        /// <summary>
        /// Update Profile Picture
        /// </summary>
        /// <param name="request"></param>
        /// <returns>Status 200 OK</returns>
        // La administración de usuarios sube la foto después de crear / editar (multipart, campo "request"). El servicio
        // exige que el usuario sea gestionable por quien la sube (UserAdminScope) y que sea una imagen válida.
        [Authorize(Policy = DS.Platform.Shared.Constants.Permission.Permissions.Users.Edit)]
        [HttpPost("profile-picture/{userId}")]
        public async Task<IActionResult> UpdateProfilePictureAsync(string userId, IFormFile request)
        {
            return Ok(await _accountService.UpdateProfilePictureAsync(request, userId));
        }
    }
}