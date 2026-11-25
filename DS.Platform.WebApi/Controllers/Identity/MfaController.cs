using DS.Platform.Application.DTOs.Identities.Request;
using DS.Platform.Application.Interfaces.Services;
using DS.Platform.Application.Interfaces.Services.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;

namespace DS.Platform.WebApi.Controllers.Identity
{
    /// <summary>Mi cuenta → Seguridad: estado del MFA y regenerar códigos de recuperación (Fase 12a).</summary>
    [Authorize]
    [Route("api/identity/mfa")]
    [ApiController]
    public class MfaController : ControllerBase
    {
        private readonly IMfaService _mfa;
        private readonly ICurrentUserService _currentUser;

        public MfaController(IMfaService mfa, ICurrentUserService currentUser)
        {
            _mfa = mfa;
            _currentUser = currentUser;
        }

        [HttpGet("status")]
        public async Task<IActionResult> Status()
            => Ok(await _mfa.GetStatusAsync(_currentUser.UserId!));

        [HttpPost("recovery-codes")]
        public async Task<IActionResult> RegenerateRecoveryCodes(MfaCodeRequest request)
            => Ok(await _mfa.RegenerateRecoveryCodesAsync(_currentUser.UserId!, request.Code));
    }
}
