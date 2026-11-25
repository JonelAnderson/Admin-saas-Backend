using DS.Platform.Application.DTOs.Identities.Request;
using DS.Platform.Application.Interfaces.Services;
using DS.Platform.Application.Interfaces.Services.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;

namespace DS.Platform.WebApi.Controllers.Identity
{
    [Route("api/identity")]
    [ApiController]
    public class TokenController : ControllerBase
    {
        private readonly ITokenService _identityService;

        public TokenController(ITokenService identityService, ICurrentUserService currentUserService)
        {
            _identityService = identityService;
          
        }

        /// <summary>
        /// Get Token (User, Password)
        /// </summary>
        /// <param name="model"></param>
        /// <returns>Status 200 OK</returns>
        [AllowAnonymous]
        [HttpPost("login")]
        public async Task<ActionResult> Get(TokenRequest model)
        {
           
           var response = await _identityService.LoginAsync(model);
            return Ok(response);
        }

        /// <summary>
        /// Paso 2 del login: ticket + código de la app (o de recuperación) → token (amr = mfa).
        /// </summary>
        [AllowAnonymous]
        [HttpPost("login/mfa")]
        public async Task<ActionResult> LoginMfa(MfaLoginRequest model)
        {
            return Ok(await _identityService.LoginMfaAsync(model));
        }

        /// <summary>
        /// Enrolamiento: clave nueva del autenticador y su URI otpauth (el frontend dibuja el QR).
        /// </summary>
        [AllowAnonymous]
        [HttpPost("login/mfa/setup")]
        public async Task<ActionResult> BeginMfaSetup(MfaTicketRequest model)
        {
            return Ok(await _identityService.BeginMfaSetupAsync(model));
        }

        /// <summary>
        /// Enrolamiento: primer código → MFA activo, token y 10 códigos de recuperación.
        /// </summary>
        [AllowAnonymous]
        [HttpPost("login/mfa/setup/confirm")]
        public async Task<ActionResult> ConfirmMfaSetup(MfaLoginRequest model)
        {
            return Ok(await _identityService.ConfirmMfaSetupAsync(model));
        }

        /// <summary>
        /// Refresh Token
        /// </summary>
        /// <param name="model"></param>
        /// <returns>Status 200 OK</returns>
        [AllowAnonymous]
        [HttpPost("refreshToken")]
        public async Task<ActionResult> Refresh([FromBody] RefreshTokenRequest model)
        {
            var response = await _identityService.GetRefreshTokenAsync(model);
            return Ok(response);
        }

        /// <summary>
        /// Cerrar sesión en todos los dispositivos del usuario del token: borra su refresh token y revoca sus JWT (nuevo security
        /// stamp). El usuario sale del propio token, nunca de un parámetro.
        /// </summary>
        [Authorize]
        [HttpPost("logout")]
        public async Task<ActionResult> Logout()
        {
            var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? string.Empty;
            return Ok(await _identityService.LogoutAsync(userId));
        }
    }
}