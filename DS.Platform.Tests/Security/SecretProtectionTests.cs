using DS.Platform.Infrastructure.Persistence.Services.Security;
using Microsoft.AspNetCore.DataProtection;

namespace DS.Platform.Tests.Security
{
    public class SecretProtectionTests
    {
        private const string Token = "api-key-123";

        private readonly DataProtectionSecretProtector _protector = new(new EphemeralDataProtectionProvider());

        [Fact]
        public void Protect_round_trips_and_hides_the_plaintext()
        {
            var stored = _protector.Protect(Token);

            Assert.StartsWith(DataProtectionSecretProtector.Prefix, stored);
            Assert.DoesNotContain(Token, stored);
            Assert.True(_protector.IsProtected(stored));
            Assert.Equal(Token, _protector.Unprotect(stored));
        }

        [Fact]
        public void Each_protect_call_produces_a_different_ciphertext()
        {
            Assert.NotEqual(_protector.Protect(Token), _protector.Protect(Token));
        }

        [Fact]
        public void Legacy_plaintext_is_returned_as_is()
        {
            Assert.False(_protector.IsProtected(Token));
            Assert.Equal(Token, _protector.Unprotect(Token));
        }

        [Fact]
        public void Lost_key_ring_fails_with_an_explicit_error()
        {
            var stored = _protector.Protect(Token);
            var otherKeys = new DataProtectionSecretProtector(new EphemeralDataProtectionProvider());

            var ex = Assert.Throws<InvalidOperationException>(() => otherKeys.Unprotect(stored));
            Assert.Contains("key ring", ex.Message);
        }
    }
}
