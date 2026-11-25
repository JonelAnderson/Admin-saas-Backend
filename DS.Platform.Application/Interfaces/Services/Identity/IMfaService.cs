using DS.Platform.Application.DTOs.Identities.Response;
using DS.Platform.Application.Interfaces.Common;
using DS.Platform.Domain.Entities.Identity;
using DS.Platform.Shared.Wrapper;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DS.Platform.Application.Interfaces.Services.Identity
{
    /// <summary>
    /// Autenticación en dos pasos con app TOTP (Fase 12a): enrolamiento, códigos de recuperación, restablecer y desbloquear.
    /// Los pasos anónimos del login (tickets, JWT) los orquesta <see cref="ITokenService"/>.
    /// </summary>
    public interface IMfaService : IService
    {
        /// <summary>Genera y guarda (cifrada) una clave nueva; la anterior deja de servir. No cambia el security stamp.</summary>
        Task<MfaSetupResponse> CreateAuthenticatorKeyAsync(AdmUser user);

        /// <summary>Valida un código de 6 dígitos contra la clave del usuario (no cuenta fallos).</summary>
        Task<bool> VerifyAuthenticatorCodeAsync(AdmUser user, string? code);

        /// <summary>Si el código es válido activa el MFA y devuelve los 10 códigos de recuperación; si no, null.</summary>
        Task<List<string>?> EnableAsync(AdmUser user, string? code);

        Task<Result<MfaStatusResponse>> GetStatusAsync(string userId);

        /// <summary>Con el código actual de la app, reemplaza los códigos de recuperación por 10 nuevos.</summary>
        Task<Result<List<string>>> RegenerateRecoveryCodesAsync(string userId, string? code);

        /// <summary>Administrador: desactiva el MFA de otro usuario, borra su clave y códigos, nuevo security stamp y sin refresh token.</summary>
        Task<IResult> ResetAsync(string userId);

        /// <summary>Administrador: quita el bloqueo por intentos fallidos.</summary>
        Task<IResult> UnlockAsync(string userId);
    }
}
