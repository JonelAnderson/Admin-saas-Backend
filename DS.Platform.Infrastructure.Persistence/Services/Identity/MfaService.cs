using DS.Platform.Application.DTOs.Identities.Response;
using DS.Platform.Application.Interfaces.Services;
using DS.Platform.Application.Interfaces.Services.Identity;
using DS.Platform.Domain.Entities.Identity;
using DS.Platform.Shared.Constants.User;
using DS.Platform.Shared.Wrapper;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using System.Text;

namespace DS.Platform.Infrastructure.Services.Identity
{
    /// <summary>
    /// MFA con app autenticadora TOTP (Fase 12a). Infraestructura de Identity (§3 de CLAUDE.md): la clave va cifrada y
    /// los códigos de recuperación con hash en <see cref="AdmUserStore"/>. Los eventos se registran sin secretos.
    /// </summary>
    public class MfaService : IMfaService
    {
        public const string Issuer = "DS.Platform";
        public const int RecoveryCodeCount = 10;

        private readonly UserManager<AdmUser> _userManager;
        private readonly IUserStore<AdmUser> _userStore;
        private readonly ICurrentUserService _currentUser;
        private readonly ILogger<MfaService> _logger;
        private readonly ISessionStampService _sessions;
        private readonly ITrustedDeviceService _devices;

        public MfaService(UserManager<AdmUser> userManager, IUserStore<AdmUser> userStore,
            ICurrentUserService currentUser, ILogger<MfaService> logger, ISessionStampService sessions, ITrustedDeviceService devices)
        {
            _devices = devices;
            _sessions = sessions;
            _userManager = userManager;
            _userStore = userStore;
            _currentUser = currentUser;
            _logger = logger;
        }

        public async Task<MfaSetupResponse> CreateAuthenticatorKeyAsync(AdmUser user)
        {
            // Directo al store: UserManager.ResetAuthenticatorKeyAsync cambia el security stamp y anularía el ticket del enrolamiento.
            if (_userStore is not IUserAuthenticatorKeyStore<AdmUser> keyStore)
            {
                throw new NotSupportedException("The user store does not support authenticator keys.");
            }

            var key = _userManager.GenerateNewAuthenticatorKey();
            await keyStore.SetAuthenticatorKeyAsync(user, key, CancellationToken.None);
            var saved = await _userManager.UpdateAsync(user);
            if (!saved.Succeeded)
            {
                throw new InvalidOperationException(string.Join(" ", saved.Errors.Select(e => e.Description)));
            }

            _logger.LogInformation("MFA: clave de autenticador generada para el usuario {UserId}", user.Id);
            return new MfaSetupResponse
            {
                SharedKey = FormatKey(key),
                AuthenticatorUri = AuthenticatorUri(user.UserName ?? user.Email ?? user.Id, key)
            };
        }

        public async Task<bool> VerifyAuthenticatorCodeAsync(AdmUser user, string? code)
        {
            var normalized = NormalizeTotp(code);
            return normalized is not null
                && await _userManager.VerifyTwoFactorTokenAsync(user, _userManager.Options.Tokens.AuthenticatorTokenProvider, normalized);
        }

        public async Task<List<string>?> EnableAsync(AdmUser user, string? code)
        {
            if (!await VerifyAuthenticatorCodeAsync(user, code))
            {
                return null;
            }

            await _userManager.SetTwoFactorEnabledAsync(user, true);
            var codes = (await _userManager.GenerateNewTwoFactorRecoveryCodesAsync(user, RecoveryCodeCount))?.ToList() ?? new List<string>();
            _logger.LogInformation("MFA: activado para el usuario {UserId}", user.Id);
            return codes;
        }

        public async Task<Result<MfaStatusResponse>> GetStatusAsync(string userId)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user is null)
            {
                return await Result<MfaStatusResponse>.FailAsync(LoginMessages.UserNotFound);
            }

            return await Result<MfaStatusResponse>.SuccessAsync(new MfaStatusResponse
            {
                Enabled = user.TwoFactorEnabled,
                RecoveryCodesLeft = user.TwoFactorEnabled ? await _userManager.CountRecoveryCodesAsync(user) : 0
            });
        }

        public async Task<Result<List<string>>> RegenerateRecoveryCodesAsync(string userId, string? code)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user is null)
            {
                return await Result<List<string>>.FailAsync(LoginMessages.UserNotFound);
            }
            if (!user.TwoFactorEnabled)
            {
                return await Result<List<string>>.FailAsync(LoginMessages.MfaNotEnabled);
            }
            if (await _userManager.IsLockedOutAsync(user))
            {
                return await Result<List<string>>.FailAsync(LoginMessages.LockedUntil(user.LockoutEnd!.Value));
            }
            if (!await VerifyAuthenticatorCodeAsync(user, code))
            {
                // Cuenta el fallo: un JWT robado no sirve para probar códigos sin límite.
                await _userManager.AccessFailedAsync(user);
                _logger.LogWarning("MFA: código incorrecto al regenerar códigos de recuperación del usuario {UserId}", user.Id);
                return await Result<List<string>>.FailAsync(await _userManager.IsLockedOutAsync(user)
                    ? LoginMessages.LockedUntil(user.LockoutEnd!.Value)
                    : LoginMessages.InvalidCode);
            }

            await _userManager.ResetAccessFailedCountAsync(user);
            var codes = (await _userManager.GenerateNewTwoFactorRecoveryCodesAsync(user, RecoveryCodeCount))?.ToList() ?? new List<string>();
            _logger.LogInformation("MFA: códigos de recuperación regenerados por el usuario {UserId}", user.Id);
            return await Result<List<string>>.SuccessAsync(codes);
        }

        public async Task<IResult> ResetAsync(string userId)
        {
            if (string.Equals(userId, _currentUser.UserId, StringComparison.Ordinal))
            {
                return await Result.FailAsync(LoginMessages.CannotResetOwnMfa);
            }

            var user = await _userManager.FindByIdAsync(userId);
            if (user is null)
            {
                return await Result.FailAsync(LoginMessages.UserNotFound);
            }
            if (!await UserAdminScope.CanManageAsync(_userManager, _currentUser, user))
            {
                return await Result.FailAsync(UserAdminScope.DeniedMessage);
            }

            await _userManager.SetTwoFactorEnabledAsync(user, false);
            await _userManager.RemoveAuthenticationTokenAsync(user, AdmUserStore.InternalLoginProvider, AdmUserStore.AuthenticatorKeyTokenName);
            await _userManager.RemoveAuthenticationTokenAsync(user, AdmUserStore.InternalLoginProvider, AdmUserStore.RecoveryCodeTokenName);
            user.RefreshToken = null;
            // Nuevo stamp: invalida tickets de MFA pendientes; sin refresh token la sesión actual no se renueva.
            await _userManager.UpdateSecurityStampAsync(user);
            // Sus JWT abiertos (todos los dispositivos) dejan de valer en el siguiente request.
            _sessions.Invalidate(user.Id);
            // Con el MFA nuevo, ningún dispositivo queda recordado.
            await _devices.RevokeAllAsync(user.Id);

            _logger.LogWarning("MFA: restablecido para el usuario {UserId} por {AdminId}", user.Id, _currentUser.UserId);
            return await Result.SuccessAsync(LoginMessages.MfaReset);
        }

        public async Task<IResult> UnlockAsync(string userId)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user is null)
            {
                return await Result.FailAsync(LoginMessages.UserNotFound);
            }
            if (!await UserAdminScope.CanManageAsync(_userManager, _currentUser, user))
            {
                return await Result.FailAsync(UserAdminScope.DeniedMessage);
            }

            user.LockoutEnd = null;
            user.AccessFailedCount = 0;
            var saved = await _userManager.UpdateAsync(user);
            if (!saved.Succeeded)
            {
                return await Result.FailAsync(saved.Errors.Select(e => e.Description).ToList());
            }

            _logger.LogInformation("Login: cuenta del usuario {UserId} desbloqueada por {AdminId}", user.Id, _currentUser.UserId);
            return await Result.SuccessAsync(LoginMessages.Unlocked);
        }

        /// <summary>Solo dígitos (se aceptan espacios y guiones al escribir); null si no son 6.</summary>
        public static string? NormalizeTotp(string? code)
        {
            if (string.IsNullOrWhiteSpace(code))
            {
                return null;
            }
            var digits = code.Replace(" ", string.Empty).Replace("-", string.Empty);
            return digits.Length == 6 && digits.All(char.IsAsciiDigit) ? digits : null;
        }

        /// <summary>"ABCD EFGH …" en mayúsculas, para ingresarla a mano.</summary>
        public static string FormatKey(string key)
        {
            var upper = key.ToUpperInvariant();
            var result = new StringBuilder();
            for (var i = 0; i < upper.Length; i += 4)
            {
                if (i > 0)
                {
                    result.Append(' ');
                }
                result.Append(upper.AsSpan(i, Math.Min(4, upper.Length - i)));
            }
            return result.ToString();
        }

        public static string AuthenticatorUri(string userName, string key)
            => $"otpauth://totp/{Uri.EscapeDataString(Issuer)}:{Uri.EscapeDataString(userName)}" +
               $"?secret={key.ToUpperInvariant()}&issuer={Uri.EscapeDataString(Issuer)}&digits=6&period=30";
    }
}
