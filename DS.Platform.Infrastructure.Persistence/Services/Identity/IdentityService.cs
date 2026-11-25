using DS.Platform.Application.Configurations;
using DS.Platform.Application.DTOs.Identities.Request;
using DS.Platform.Application.DTOs.Identities.Response;
using DS.Platform.Application.Interfaces.Repositories;
using DS.Platform.Application.Interfaces.Services.Identity;
using DS.Platform.Domain.Entities;
using DS.Platform.Domain.Entities.Identity;
using DS.Platform.Shared.Constants.Permission;
using DS.Platform.Shared.Constants.User;
using DS.Platform.Shared.Wrapper;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace DS.Platform.Infrastructure.Services.Identity
{
    /// <summary>
    /// Login en dos pasos (Fase 12a): contraseña → ticket de MFA → código TOTP / de recuperación → JWT con <c>amr = mfa</c>.
    /// Nunca se emite un JWT sin MFA. Los fallos de contraseña y de código cuentan para el bloqueo (5 → 15 minutos).
    /// </summary>
    public class IdentityService : ITokenService
    {
        /// <summary>Claim del JWT que prueba que la sesión pasó por el segundo factor (RFC 8176).</summary>
        public const string AmrClaimType = "amr";
        public const string AmrMfa = "mfa";

        private static readonly Lazy<string> DummyPasswordHash =
            new(() => new PasswordHasher<AdmUser>().HashPassword(new AdmUser(), Guid.NewGuid().ToString("N")));

        private readonly UserManager<AdmUser> _userManager;
        private readonly RoleManager<AdmRole> _roleManager;
        private readonly AppConfiguration _appConfig;
        private readonly IUnitOfWork<int> _unitOfWork;
        private readonly IMfaService _mfa;
        private readonly MfaTicketProtector _tickets;
        private readonly ISessionStampService _sessions;
        private readonly ITrustedDeviceService _devices;
        private readonly ILogger<IdentityService> _logger;

        public IdentityService(
            UserManager<AdmUser> userManager, RoleManager<AdmRole> roleManager,
            IOptions<AppConfiguration> appConfig,
            IUnitOfWork<int> unitOfWork,
            IMfaService mfa,
            MfaTicketProtector tickets,
            ISessionStampService sessions,
            ITrustedDeviceService devices,
            ILogger<IdentityService> logger)
        {
            _devices = devices;
            _userManager = userManager;
            _roleManager = roleManager;
            _appConfig = appConfig.Value;
            _unitOfWork = unitOfWork;
            _mfa = mfa;
            _tickets = tickets;
            _sessions = sessions;
            _logger = logger;
        }

        public async Task<IResult> LogoutAsync(string userId)
        {
            var user = string.IsNullOrEmpty(userId) ? null : await _userManager.FindByIdAsync(userId);
            if (user is null)
            {
                return await Result.FailAsync(LoginMessages.UserNotFound);
            }

            user.RefreshToken = null;
            user.RefreshTokenExpiryTime = DateTime.UtcNow;
            await _userManager.UpdateAsync(user);
            // Nuevo security stamp: los JWT de todos los dispositivos del usuario dejan de valer.
            await _userManager.UpdateSecurityStampAsync(user);
            _sessions.Invalidate(user.Id);
            _logger.LogInformation("Logout: sesiones del usuario {UserId} cerradas en todos los dispositivos", user.Id);
            return await Result.SuccessAsync("Sesión cerrada.");
        }

        public async Task<Result<TokenResponse>> LoginAsync(TokenRequest model)
        {
            var user = string.IsNullOrWhiteSpace(model.UserName) ? null : await _userManager.FindByNameAsync(model.UserName.Trim());
            if (user is null || user.IsDeleted)
            {
                // Mismo costo que una verificación real: el tiempo de respuesta no revela si el usuario existe.
                _userManager.PasswordHasher.VerifyHashedPassword(new AdmUser(), DummyPasswordHash.Value, model.Password ?? string.Empty);
                _logger.LogWarning("Login: intento con un usuario inexistente");
                return await Result<TokenResponse>.FailAsync(LoginMessages.InvalidCredentials);
            }

            if (!user.LockoutEnabled)
            {
                await _userManager.SetLockoutEnabledAsync(user, true);
            }
            if (await _userManager.IsLockedOutAsync(user))
            {
                _logger.LogWarning("Login: usuario {UserId} bloqueado intentó ingresar", user.Id);
                return await Result<TokenResponse>.FailAsync(LoginMessages.LockedUntil(user.LockoutEnd!.Value));
            }

            if (!await _userManager.CheckPasswordAsync(user, model.Password ?? string.Empty))
            {
                return await FailedAttemptAsync(user, LoginMessages.InvalidCredentials, "contraseña incorrecta");
            }

            // Con la contraseña correcta se mantienen los mensajes específicos.
            if (!user.IsActive)
            {
                return await Result<TokenResponse>.FailAsync(LoginMessages.UserInactive);
            }
            if (!user.EmailConfirmed)
            {
                return await Result<TokenResponse>.FailAsync(LoginMessages.EmailNotConfirmed);
            }

            // Dispositivo recordado (entró con MFA desde aquí y no pasaron 7 días sin entrar): no se vuelve a pedir el código.
            if (user.TwoFactorEnabled && await _devices.IsTrustedAsync(user.Id, model.DeviceToken))
            {
                var trusted = await IssueTokensAsync(user);
                trusted.DeviceTrusted = true;
                _logger.LogInformation("Login: usuario {UserId} ingresó desde un dispositivo recordado", user.Id);
                return await Result<TokenResponse>.SuccessAsync(trusted, LoginMessages.LoginOk);
            }

            var stage = user.TwoFactorEnabled ? MfaTicketProtector.StageVerify : MfaTicketProtector.StageSetup;
            var ticket = _tickets.Create(user.Id, await GetSecurityStampAsync(user), stage);
            _logger.LogInformation("Login: contraseña correcta para el usuario {UserId}; pendiente MFA ({Stage})", user.Id, stage);

            return await Result<TokenResponse>.SuccessAsync(new TokenResponse
            {
                MfaRequired = user.TwoFactorEnabled,
                MfaSetupRequired = !user.TwoFactorEnabled,
                MfaTicket = ticket
            });
        }

        public async Task<Result<TokenResponse>> LoginMfaAsync(MfaLoginRequest model)
        {
            var (user, error) = await ReadTicketAsync(model?.MfaTicket, MfaTicketProtector.StageVerify);
            if (user is null)
            {
                return await Result<TokenResponse>.FailAsync(error!);
            }
            if (!user.TwoFactorEnabled)
            {
                return await Result<TokenResponse>.FailAsync(LoginMessages.TicketExpired);
            }

            bool valid;
            var recovery = false;
            if (!string.IsNullOrWhiteSpace(model!.Code))
            {
                valid = await _mfa.VerifyAuthenticatorCodeAsync(user, model.Code);
            }
            else if (!string.IsNullOrWhiteSpace(model.RecoveryCode))
            {
                recovery = true;
                valid = (await _userManager.RedeemTwoFactorRecoveryCodeAsync(user, model.RecoveryCode.Trim())).Succeeded;
            }
            else
            {
                return await Result<TokenResponse>.FailAsync(LoginMessages.CodeRequired);
            }

            if (!valid)
            {
                return await FailedAttemptAsync(user, LoginMessages.InvalidCode,
                    recovery ? "código de recuperación incorrecto" : "código MFA incorrecto");
            }

            var response = await IssueTokensAsync(user);
            response.RecoveryCodesLeft = await _userManager.CountRecoveryCodesAsync(user);
            if (model.RememberDevice)
            {
                response.DeviceToken = await _devices.TrustAsync(user.Id, model.DeviceLabel);
            }
            if (recovery)
            {
                _logger.LogWarning("MFA: el usuario {UserId} ingresó con un código de recuperación; le quedan {Left}",
                    user.Id, response.RecoveryCodesLeft);
            }
            else
            {
                _logger.LogInformation("Login: usuario {UserId} ingresó con MFA", user.Id);
            }
            return await Result<TokenResponse>.SuccessAsync(response, LoginMessages.LoginOk);
        }

        public async Task<Result<MfaSetupResponse>> BeginMfaSetupAsync(MfaTicketRequest model)
        {
            var (user, error) = await ReadTicketAsync(model?.MfaTicket, MfaTicketProtector.StageSetup);
            if (user is null)
            {
                return await Result<MfaSetupResponse>.FailAsync(error!);
            }
            if (user.TwoFactorEnabled)
            {
                return await Result<MfaSetupResponse>.FailAsync(LoginMessages.TicketExpired);
            }

            return await Result<MfaSetupResponse>.SuccessAsync(await _mfa.CreateAuthenticatorKeyAsync(user));
        }

        public async Task<Result<TokenResponse>> ConfirmMfaSetupAsync(MfaLoginRequest model)
        {
            var (user, error) = await ReadTicketAsync(model?.MfaTicket, MfaTicketProtector.StageSetup);
            if (user is null)
            {
                return await Result<TokenResponse>.FailAsync(error!);
            }
            if (user.TwoFactorEnabled)
            {
                return await Result<TokenResponse>.FailAsync(LoginMessages.TicketExpired);
            }
            if (string.IsNullOrWhiteSpace(model!.Code))
            {
                return await Result<TokenResponse>.FailAsync(LoginMessages.CodeRequired);
            }
            if (string.IsNullOrEmpty(await _userManager.GetAuthenticatorKeyAsync(user)))
            {
                return await Result<TokenResponse>.FailAsync(LoginMessages.SetupFirst);
            }

            var codes = await _mfa.EnableAsync(user, model.Code);
            if (codes is null)
            {
                return await FailedAttemptAsync(user, LoginMessages.InvalidCode, "código incorrecto al enrolarse");
            }

            var response = await IssueTokensAsync(user);
            response.RecoveryCodes = codes;
            response.RecoveryCodesLeft = codes.Count;
            if (model.RememberDevice)
            {
                response.DeviceToken = await _devices.TrustAsync(user.Id, model.DeviceLabel);
            }
            return await Result<TokenResponse>.SuccessAsync(response, LoginMessages.LoginOk);
        }

        public async Task<Result<TokenResponse>> GetRefreshTokenAsync(RefreshTokenRequest model)
        {
            if (model is null || string.IsNullOrWhiteSpace(model.Token) || string.IsNullOrWhiteSpace(model.RefreshToken))
            {
                return await Result<TokenResponse>.FailAsync(LoginMessages.SessionExpired);
            }

            ClaimsPrincipal principal;
            JwtSecurityToken jwt;
            try
            {
                (principal, jwt) = GetPrincipalFromExpiredToken(model.Token);
            }
            catch (Exception)
            {
                return await Result<TokenResponse>.FailAsync(LoginMessages.SessionExpired);
            }

            // Tokens emitidos sin segundo factor (antes de la Fase 12a) no se renuevan: el usuario vuelve al login.
            if (!jwt.Claims.Any(c => c.Type == AmrClaimType && c.Value == AmrMfa))
            {
                _logger.LogInformation("Login: refresh rechazado, token sin MFA");
                return await Result<TokenResponse>.FailAsync(LoginMessages.SessionExpired);
            }

            var userId = principal.FindFirstValue(ClaimTypes.NameIdentifier);
            var user = string.IsNullOrEmpty(userId) ? null : await _userManager.FindByIdAsync(userId);
            // El bloqueo por intentos fallidos frena el adivinar contraseñas y códigos, no la sesión ya abierta con MFA: si no, un
            // anónimo que falla 5 veces con el usuario de otro le cortaba la renovación (auditoría run-1). La revocación va por el sello.
            if (user is null || user.IsDeleted || !user.IsActive || !user.TwoFactorEnabled)
            {
                return await Result<TokenResponse>.FailAsync(LoginMessages.SessionExpired);
            }
            if (string.IsNullOrEmpty(user.RefreshToken)
                || !FixedTimeEquals(user.RefreshToken, model.RefreshToken)
                || user.RefreshTokenExpiryTime <= DateTime.UtcNow)
            {
                return await Result<TokenResponse>.FailAsync(LoginMessages.SessionExpired);
            }

            var token = GenerateEncryptedToken(GetSigningCredentials(), await GetClaimsAsync(user));
            user.RefreshToken = GenerateRefreshToken();
            await _userManager.UpdateAsync(user);

            var response = new TokenResponse { Token = token, RefreshToken = user.RefreshToken, RefreshTokenExpiryTime = user.RefreshTokenExpiryTime };
            return await Result<TokenResponse>.SuccessAsync(response);
        }

        public async Task<Result<List<string>>> GetPermissionsAsync(string userId)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null) return await Result<List<string>>.FailAsync(LoginMessages.UserNotFound);

            var roles = await _userManager.GetRolesAsync(user);
            var permissions = new List<string>();

            foreach (var role in roles)
            {
                var roleEntity = await _roleManager.FindByNameAsync(role);
                if (roleEntity != null)
                {
                    var roleClaims = await _roleManager.GetClaimsAsync(roleEntity);
                    foreach (var claim in roleClaims)
                    {
                        if (claim.Type == ApplicationClaimTypes.Permission)
                        {
                            permissions.Add(claim.Value);
                        }
                    }
                }
            }

            return await Result<List<string>>.SuccessAsync(permissions.Distinct().ToList());
        }

        /// <summary>Ticket válido, de la etapa pedida, del mismo security stamp y de un usuario activo y no bloqueado.</summary>
        private async Task<(AdmUser? User, string? Error)> ReadTicketAsync(string? rawTicket, string stage)
        {
            var ticket = _tickets.Read(rawTicket);
            if (ticket is null || ticket.Stage != stage)
            {
                return (null, LoginMessages.TicketExpired);
            }

            var user = await _userManager.FindByIdAsync(ticket.UserId);
            if (user is null || user.IsDeleted || !FixedTimeEquals(user.SecurityStamp ?? string.Empty, ticket.SecurityStamp))
            {
                return (null, LoginMessages.TicketExpired);
            }
            if (!user.IsActive)
            {
                return (null, LoginMessages.UserInactive);
            }
            if (!user.EmailConfirmed)
            {
                return (null, LoginMessages.EmailNotConfirmed);
            }
            if (await _userManager.IsLockedOutAsync(user))
            {
                return (null, LoginMessages.LockedUntil(user.LockoutEnd!.Value));
            }
            return (user, null);
        }

        private async Task<Result<TokenResponse>> FailedAttemptAsync(AdmUser user, string message, string reason)
        {
            await _userManager.AccessFailedAsync(user);
            if (await _userManager.IsLockedOutAsync(user))
            {
                _logger.LogWarning("Login: usuario {UserId} bloqueado hasta {LockoutEnd:o} ({Reason})", user.Id, user.LockoutEnd, reason);
                return await Result<TokenResponse>.FailAsync(LoginMessages.LockedUntil(user.LockoutEnd!.Value));
            }

            _logger.LogWarning("Login: {Reason} para el usuario {UserId} ({Failed} fallos seguidos)", reason, user.Id, user.AccessFailedCount);
            return await Result<TokenResponse>.FailAsync(message);
        }

        /// <summary>Login completo: reinicia el contador de fallos, nuevo refresh token (1 días) y JWT con amr = mfa.</summary>
        private async Task<TokenResponse> IssueTokensAsync(AdmUser user)
        {
            await _userManager.ResetAccessFailedCountAsync(user);
            user.RefreshToken = GenerateRefreshToken();
            user.RefreshTokenExpiryTime = DateTime.UtcNow.AddDays(1);
            await _userManager.UpdateAsync(user);

            var token = GenerateEncryptedToken(GetSigningCredentials(), await GetClaimsAsync(user));
            return new TokenResponse
            {
                Token = token,
                RefreshToken = user.RefreshToken,
                UserImageURL = user.ProfilePictureDataUrl,
                RefreshTokenExpiryTime = user.RefreshTokenExpiryTime
            };
        }

        private async Task<string> GetSecurityStampAsync(AdmUser user)
        {
            if (string.IsNullOrEmpty(user.SecurityStamp))
            {
                await _userManager.UpdateSecurityStampAsync(user);
            }
            return user.SecurityStamp!;
        }

        private async Task<IEnumerable<Claim>> GetClaimsAsync(AdmUser user)
        {
            var userClaims = await _userManager.GetClaimsAsync(user);
            var roles = await _userManager.GetRolesAsync(user);
            var roleClaims = new List<Claim>();

            foreach (var role in roles)
            {
                roleClaims.Add(new Claim(ClaimTypes.Role, role));
            }

            // La consola no es multiempresa: el token no lleva empresa ni sucursal.
            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, user.Id),
                // Sin datos personales (correo, usuario, celular, foto): el JWT se guarda en el navegador y cualquiera lo
                // decodifica. La pantalla los pide a GET api/identity/account/me.
                new(AmrClaimType, AmrMfa),
                // Sello de sesión: si el security stamp cambia (logout, contraseña, MFA, roles), este token deja de valer.
                new(SessionStampService.ClaimType, SessionStampService.Hash(await GetSecurityStampAsync(user)))
            }
            .Union(userClaims)
            .Union(roleClaims);

            return claims;
        }

        /// <summary>
        /// Vida del token de acceso: <c>AccessTokenMinutes</c> (30 por defecto); el frontend lo renueva solo con el refresh token.
        /// <c>Expires</c> (horas) queda solo como respaldo de configuraciones antiguas.
        /// </summary>
        internal static int AccessTokenMinutes(AppConfiguration config)
        {
            if (config.AccessTokenMinutes > 0)
                return config.AccessTokenMinutes;
            return int.TryParse(config.Expires, out var hours) && hours > 0 ? hours * 60 : 30;
        }

        private static string GenerateRefreshToken()
        {
            var randomNumber = new byte[32];
            using var rng = RandomNumberGenerator.Create();
            rng.GetBytes(randomNumber);
            return Convert.ToBase64String(randomNumber);
        }

        private static bool FixedTimeEquals(string a, string b)
            => CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(a), Encoding.UTF8.GetBytes(b));

        private string GenerateEncryptedToken(SigningCredentials signingCredentials, IEnumerable<Claim> claims)
        {
            var token = new JwtSecurityToken(
               issuer: _appConfig.Issuer,
               audience: _appConfig.Issuer,
               claims: claims,
               expires: DateTime.UtcNow.AddMinutes(AccessTokenMinutes(_appConfig)),
               notBefore: DateTime.UtcNow,
               signingCredentials: signingCredentials);
            var tokenHandler = new JwtSecurityTokenHandler();
            var encryptedToken = tokenHandler.WriteToken(token);
            return encryptedToken;
        }

        private (ClaimsPrincipal Principal, JwtSecurityToken Jwt) GetPrincipalFromExpiredToken(string token)
        {
            var tokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_appConfig.Secret!)),
                ValidateIssuer = true,
                ValidIssuer = _appConfig.Issuer,
                ValidateAudience = true,
                ValidAudience = _appConfig.Issuer,
                RoleClaimType = ClaimTypes.Role,
                ClockSkew = TimeSpan.Zero,
                ValidateLifetime = false
            };
            var tokenHandler = new JwtSecurityTokenHandler();
            var principal = tokenHandler.ValidateToken(token, tokenValidationParameters, out var securityToken);
            if (securityToken is not JwtSecurityToken jwtSecurityToken || !jwtSecurityToken.Header.Alg.Equals(SecurityAlgorithms.HmacSha256,
                StringComparison.InvariantCultureIgnoreCase))
            {
                throw new SecurityTokenException("Invalid token");
            }

            return (principal, jwtSecurityToken);
        }

        private SigningCredentials GetSigningCredentials()
        {
            var secret = Encoding.UTF8.GetBytes(_appConfig.Secret!);
            var credential = new SigningCredentials(new SymmetricSecurityKey(secret), SecurityAlgorithms.HmacSha256);
            return credential;
        }
    }
}
