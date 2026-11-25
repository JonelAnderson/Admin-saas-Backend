using DS.Platform.Application.Interfaces.Common;

namespace DS.Platform.Application.Interfaces.Services.Security
{
    /// <summary>
    /// Encrypts secrets stored in the database (third-party API tokens, credentials).
    /// Stored format: <c>dp1:&lt;ciphertext&gt;</c>; values without the prefix are legacy plaintext.
    /// </summary>
    public interface ISecretProtector : IService
    {
        string Protect(string plaintext);

        /// <summary>Decrypts a stored value; legacy plaintext (no prefix) is returned as-is.</summary>
        string Unprotect(string storedValue);

        bool IsProtected(string? storedValue);
    }
}
