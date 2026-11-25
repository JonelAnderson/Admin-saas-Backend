using DS.Platform.Domain.Entities.Identity;
using DS.Platform.Infrastructure.Persistence.Contexts;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;

namespace DS.Platform.Infrastructure.Services.Identity
{
    /// <summary>
    /// Store de Identity con los secretos del MFA protegidos en <c>Identity.UserTokens</c> (Fase 12a):
    /// - la clave del autenticador se guarda cifrada con Data Protection (propósito <see cref="AuthenticatorKeyPurpose"/>);
    ///   <see cref="GetAuthenticatorKeyAsync"/> la devuelve descifrada, así el proveedor TOTP de Identity funciona igual;
    /// - los códigos de recuperación se guardan como hashes SHA-256 (base64) y se comparan en tiempo constante.
    /// Mismo proveedor y nombres de token que Identity (<c>[AspNetUserStore]</c>, <c>AuthenticatorKey</c>, <c>RecoveryCodes</c>).
    /// </summary>
    public class AdmUserStore : UserStore<AdmUser, AdmRole, ApplicationDbContext, string, IdentityUserClaim<string>,
        IdentityUserRole<string>, IdentityUserLogin<string>, IdentityUserToken<string>, AdmRoleClaim>
    {
        public const string InternalLoginProvider = "[AspNetUserStore]";
        public const string AuthenticatorKeyTokenName = "AuthenticatorKey";
        public const string RecoveryCodeTokenName = "RecoveryCodes";
        public const string AuthenticatorKeyPurpose = "Identity.AuthenticatorKey";

        private const char Separator = ';';

        private readonly IDataProtector _keyProtector;

        public AdmUserStore(ApplicationDbContext context, IDataProtectionProvider dataProtection, IdentityErrorDescriber? describer = null)
            : base(context, describer)
        {
            _keyProtector = dataProtection.CreateProtector(AuthenticatorKeyPurpose);
        }

        public override Task SetAuthenticatorKeyAsync(AdmUser user, string key, CancellationToken cancellationToken)
            => SetTokenAsync(user, InternalLoginProvider, AuthenticatorKeyTokenName,
                string.IsNullOrEmpty(key) ? key : _keyProtector.Protect(key), cancellationToken);

        public override async Task<string?> GetAuthenticatorKeyAsync(AdmUser user, CancellationToken cancellationToken)
        {
            var stored = await GetTokenAsync(user, InternalLoginProvider, AuthenticatorKeyTokenName, cancellationToken);
            if (string.IsNullOrEmpty(stored))
            {
                return null;
            }

            try
            {
                return _keyProtector.Unprotect(stored);
            }
            catch (CryptographicException)
            {
                // Valor en claro o anillo de claves perdido: la clave no sirve; un administrador restablece el MFA.
                return null;
            }
        }

        public override Task ReplaceCodesAsync(AdmUser user, IEnumerable<string> recoveryCodes, CancellationToken cancellationToken)
            => SetTokenAsync(user, InternalLoginProvider, RecoveryCodeTokenName,
                string.Join(Separator, recoveryCodes.Select(HashRecoveryCode)), cancellationToken);

        public override async Task<bool> RedeemCodeAsync(AdmUser user, string code, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ArgumentNullException.ThrowIfNull(user);
            if (string.IsNullOrWhiteSpace(code))
            {
                return false;
            }

            var hashes = await GetHashesAsync(user, cancellationToken);
            var candidate = Convert.FromBase64String(HashRecoveryCode(code));
            var match = -1;
            for (var i = 0; i < hashes.Count; i++)
            {
                // Recorre todos para no revelar por tiempo cuál coincidió.
                if (TryDecode(hashes[i], out var stored) && CryptographicOperations.FixedTimeEquals(stored, candidate) && match < 0)
                {
                    match = i;
                }
            }

            if (match < 0)
            {
                return false;
            }

            hashes.RemoveAt(match);
            await SetTokenAsync(user, InternalLoginProvider, RecoveryCodeTokenName, string.Join(Separator, hashes), cancellationToken);
            return true;
        }

        public override async Task<int> CountCodesAsync(AdmUser user, CancellationToken cancellationToken)
            => (await GetHashesAsync(user, cancellationToken)).Count;

        /// <summary>SHA-256 del código normalizado (sin espacios ni guiones, en mayúsculas), en base64.</summary>
        public static string HashRecoveryCode(string code)
        {
            var normalized = new string(code.Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();
            return Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(normalized)));
        }

        private async Task<List<string>> GetHashesAsync(AdmUser user, CancellationToken cancellationToken)
        {
            var merged = await GetTokenAsync(user, InternalLoginProvider, RecoveryCodeTokenName, cancellationToken);
            return string.IsNullOrEmpty(merged)
                ? new List<string>()
                : merged.Split(Separator, StringSplitOptions.RemoveEmptyEntries).ToList();
        }

        private static bool TryDecode(string value, out byte[] bytes)
        {
            bytes = new byte[32];
            return Convert.TryFromBase64String(value, bytes, out var written) && written == 32;
        }
    }
}
