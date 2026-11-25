using DS.Platform.Domain.Entities.Identity;
using DS.Platform.Infrastructure.Services.Identity;
using DS.Platform.Tests.Support;

namespace DS.Platform.Tests.Identity
{
    /// <summary>"Olvidé mi contraseña": el token del enlace (Base64Url) se decodifica antes de pasarlo a Identity.</summary>
    public class PasswordResetTokenTests
    {
        private readonly IdentityFactory _identity = new();

        private static async Task<AdmUser> NewUserAsync(IdentityScope scope)
        {
            var name = "r" + Guid.NewGuid().ToString("N")[..8];
            var user = new AdmUser { UserName = name, Email = $"{name}@tests.local", IsActive = true, CreatedBy = "tests" };
            Assert.True((await scope.Users.CreateAsync(user, IdentityFactory.Password)).Succeeded);
            return user;
        }

        [Fact]
        public async Task Link_token_round_trips_and_resets_the_password()
        {
            await using var scope = _identity.Open();
            var user = await NewUserAsync(scope);
            var link = PasswordResetToken.Encode(await scope.Users.GeneratePasswordResetTokenAsync(user));

            Assert.DoesNotContain('+', link);
            Assert.DoesNotContain('/', link);
            var token = PasswordResetToken.TryDecode(link);
            Assert.NotNull(token);

            var result = await scope.Users.ResetPasswordAsync(user, token!, "Nueva#2026x");
            Assert.True(result.Succeeded);
            Assert.True(await scope.Users.CheckPasswordAsync(user, "Nueva#2026x"));

            // El mismo enlace no sirve dos veces (cambió el security stamp).
            var again = await scope.Users.ResetPasswordAsync(user, token!, "Otra#2026xy");
            Assert.Contains(again.Errors, e => e.Code == "InvalidToken");
        }

        [Fact]
        public async Task Raw_identity_token_without_decoding_does_not_match()
        {
            await using var scope = _identity.Open();
            var user = await NewUserAsync(scope);
            var link = PasswordResetToken.Encode(await scope.Users.GeneratePasswordResetTokenAsync(user));

            var result = await scope.Users.ResetPasswordAsync(user, link, "Nueva#2026x");
            Assert.False(result.Succeeded);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("no*es%base64")]
        public void Invalid_link_text_decodes_to_null(string? text) => Assert.Null(PasswordResetToken.TryDecode(text));
    }
}
