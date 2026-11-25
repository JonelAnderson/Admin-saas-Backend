using DS.Platform.Application.Interfaces.Services.Security;
using Microsoft.AspNetCore.DataProtection;
using System.Security.Cryptography;

namespace DS.Platform.Infrastructure.Persistence.Services.Security
{
    /// <summary>
    /// ASP.NET Core Data Protection (AES-256-CBC + HMACSHA256, rotating key ring).
    /// Key ring location / protection is configured in WebApi (<c>AddErpDataProtection</c>):
    /// losing the keys makes stored secrets unreadable, so the key directory must be backed up.
    /// </summary>
    public class DataProtectionSecretProtector : ISecretProtector
    {
        public const string Prefix = "dp1:";

        // Changing the purpose invalidates every stored secret; bump the prefix instead.
        private const string Purpose = "DS.Platform.StoredSecrets.v1";

        private readonly IDataProtector _protector;

        public DataProtectionSecretProtector(IDataProtectionProvider provider)
        {
            _protector = provider.CreateProtector(Purpose);
        }

        public string Protect(string plaintext)
        {
            ArgumentException.ThrowIfNullOrEmpty(plaintext);
            return Prefix + _protector.Protect(plaintext);
        }

        public string Unprotect(string storedValue)
        {
            if (!IsProtected(storedValue))
            {
                return storedValue;
            }

            try
            {
                return _protector.Unprotect(storedValue[Prefix.Length..]);
            }
            catch (CryptographicException ex)
            {
                throw new InvalidOperationException(
                    "A stored secret could not be decrypted. The Data Protection key ring is missing or was replaced; " +
                    "restore the keys or re-enter the credential.", ex);
            }
        }

        public bool IsProtected(string? storedValue)
            => storedValue is not null && storedValue.StartsWith(Prefix, StringComparison.Ordinal);
    }
}
