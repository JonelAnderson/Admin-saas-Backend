using DS.Platform.Application.Interfaces.Services.Identity;
using DS.Platform.Shared.Constants.Permission;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;

namespace DS.Platform.WebApi.Controllers.Identity
{
    /// <summary>Administración de usuarios: restablecer MFA y desbloquear (Fase 12a). El resto sigue en <c>api/identity/user</c>.</summary>
    [Route("api/identity/users")]
    [ApiController]
    public class UserSecurityController : ControllerBase
    {
        private readonly IMfaService _mfa;

        public UserSecurityController(IMfaService mfa)
        {
            _mfa = mfa;
        }

        [Authorize(Policy = Permissions.Users.ResetMfa)]
        [HttpPost("{id}/mfa/reset")]
        public async Task<IActionResult> ResetMfa(string id)
            => Ok(await _mfa.ResetAsync(id));

        [Authorize(Policy = Permissions.Users.ResetMfa)]
        [HttpPost("{id}/unlock")]
        public async Task<IActionResult> Unlock(string id)
            => Ok(await _mfa.UnlockAsync(id));
    }
}
