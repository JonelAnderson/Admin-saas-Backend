using DS.Platform.Application.DTOs.Identities.Request;
using DS.Platform.Application.DTOs.Identities.Response;
using DS.Platform.Application.Interfaces.Common;
using DS.Platform.Shared.Wrapper;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DS.Platform.Application.Interfaces.Services.Identity
{
    public interface ITokenService : IService
    {
        /// <summary>Paso 1: usuario + contraseña. Nunca entrega un JWT: devuelve un ticket de MFA (verificar o enrolarse).</summary>
        Task<Result<TokenResponse>> LoginAsync(TokenRequest model);

        /// <summary>Paso 2: ticket + código TOTP o de recuperación → JWT (amr = mfa) + refresh token.</summary>
        Task<Result<TokenResponse>> LoginMfaAsync(MfaLoginRequest model);

        /// <summary>Enrolamiento: clave nueva del autenticador (y su URI otpauth) para el usuario del ticket.</summary>
        Task<Result<MfaSetupResponse>> BeginMfaSetupAsync(MfaTicketRequest model);

        /// <summary>Enrolamiento: valida el primer código, activa el MFA y entrega el JWT con los códigos de recuperación.</summary>
        Task<Result<TokenResponse>> ConfirmMfaSetupAsync(MfaLoginRequest model);

        Task<Result<TokenResponse>> GetRefreshTokenAsync(RefreshTokenRequest model);

        /// <summary>
        /// Cierra la sesión del usuario en todos sus dispositivos: borra el refresh token y cambia el security stamp, así los JWT
        /// que ya tenga dejan de valer en el siguiente request.
        /// </summary>
        Task<IResult> LogoutAsync(string userId);
        Task<Result<List<string>>> GetPermissionsAsync(string userId);
    }
}
