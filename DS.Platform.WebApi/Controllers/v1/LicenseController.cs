using DS.Platform.Application.DTOs.Credentials.Request;
using DS.Platform.Application.DTOs.Instances.Request;
using DS.Platform.Application.Features.Credentials.Commands.Ack;
using DS.Platform.Application.Features.Credentials.Commands.Pull;
using DS.Platform.Application.Features.Licensing.Commands.Check;
using DS.Platform.Application.Interfaces.Services.Licensing;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using System.Threading.Tasks;

namespace DS.Platform.WebApi.Controllers.v1
{
    /// <summary>
    /// Licencias de las instancias del ERP: el único endpoint de la consola expuesto a internet. Anónimo (la instancia se identifica
    /// con su clave en <c>X-License-Key</c>) y con límite de consultas por IP.
    /// </summary>
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicy)]
    public class LicenseController : BaseApiController<LicenseController>
    {
        public const string RateLimitPolicy = "license";

        /// <summary>La instancia consulta cada 10 min. Responde la licencia firmada con Ed25519 y registra el contacto y el uso.</summary>
        [HttpPost("Status")]
        public async Task<IActionResult> Status([FromBody] LicenseCheckRequestDto body, [FromHeader(Name = "X-License-Key")] string? licenseKey)
            => Ok(await _mediator.Send(new CheckLicenseCommand { LicenseKey = licenseKey, LicenseCheckRequestDto = body }));

        /// <summary>
        /// El ERP recoge las credenciales de la API SUNAT que la consola le tiene listas (fase C6b). Solo las de su instancia (por su clave);
        /// llegan en claro y se siguen guardando cifradas hasta que el ERP las confirme con <c>ApiSunat/Ack</c>.
        /// </summary>
        [HttpPost("ApiSunat/Pull")]
        public async Task<IActionResult> ApiSunatPull([FromBody] ApiSunatPullRequestDto body, [FromHeader(Name = "X-License-Key")] string? licenseKey)
        {
            // La respuesta lleva secretos: ni el navegador ni un intermediario la guardan.
            Response.Headers.CacheControl = "no-store";
            Response.Headers.Pragma = "no-cache";
            return Ok(await _mediator.Send(new PullApiSunatCredentialsCommand { LicenseKey = licenseKey, ApiSunatPullRequestDto = body }));
        }

        /// <summary>El ERP confirma las entregas que ya guardó: la consola borra su secreto. Idempotente.</summary>
        [HttpPost("ApiSunat/Ack")]
        public async Task<IActionResult> ApiSunatAck([FromBody] ApiSunatAckRequestDto body, [FromHeader(Name = "X-License-Key")] string? licenseKey)
            => Ok(await _mediator.Send(new AckApiSunatCredentialsCommand { LicenseKey = licenseKey, ApiSunatAckRequestDto = body }));

        /// <summary>Llave pública Ed25519 (32 bytes en base64) que cada ERP usa para verificar la firma. No es secreta.</summary>
        [HttpGet("PublicKey")]
        public IActionResult PublicKey([FromServices] ILicenseSigner signer)
            => Ok(new { algorithm = "Ed25519", publicKey = signer.PublicKey, configured = signer.IsConfigured });
    }
}
