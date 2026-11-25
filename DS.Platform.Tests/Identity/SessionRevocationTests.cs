using DS.Platform.Application.DTOs.Identities.Request;
using DS.Platform.Application.DTOs.Identities.Response;
using DS.Platform.Infrastructure.Services.Identity;
using DS.Platform.Tests.Support;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace DS.Platform.Tests.Identity
{
    /// <summary>
    /// Cierre de sesión real: el JWT lleva el hash del security stamp (<c>sst</c>) y cada request lo compara con el actual.
    /// Logout, cambio / restablecimiento de contraseña, restablecer MFA o desactivar al usuario revocan todos sus tokens.
    /// </summary>
    public class SessionRevocationTests
    {
        private readonly IdentityFactory _identity = new();

        private async Task<(string UserId, TokenResponse Tokens)> SignInAsync(string userName)
        {
            var userId = await _identity.CreateUserAsync(userName);
            await using var scope = _identity.Open();
            var login = await scope.Tokens.LoginAsync(new TokenRequest { UserName = userName, Password = IdentityFactory.Password });
            Assert.True(login.Succeeded, string.Join(" | ", login.Messages));
            var setup = await scope.Tokens.BeginMfaSetupAsync(new MfaTicketRequest { MfaTicket = login.Data.MfaTicket });
            var confirm = await scope.Tokens.ConfirmMfaSetupAsync(new MfaLoginRequest
            {
                MfaTicket = login.Data.MfaTicket,
                Code = Totp.Code(setup.Data.SharedKey)
            });
            Assert.True(confirm.Succeeded, string.Join(" | ", confirm.Messages));
            return (userId, confirm.Data);
        }

        private static JwtSecurityToken Read(string jwt) => new JwtSecurityTokenHandler().ReadJwtToken(jwt);

        private static string? Stamp(string jwt) => Read(jwt).Claims.FirstOrDefault(c => c.Type == SessionStampService.ClaimType)?.Value;

        private static string UserIdOf(string jwt) => Read(jwt).Claims.First(c => c.Type == ClaimTypes.NameIdentifier).Value;

        [Fact]
        public async Task Token_carries_the_session_stamp_and_lasts_30_minutes()
        {
            var (userId, tokens) = await SignInAsync("ana");

            Assert.Equal(userId, UserIdOf(tokens.Token!));
            Assert.False(string.IsNullOrEmpty(Stamp(tokens.Token!)));
            var minutes = (Read(tokens.Token!).ValidTo - DateTime.UtcNow).TotalMinutes;
            Assert.InRange(minutes, 29, 30.5);

            await using var scope = _identity.Open();
            Assert.True(await scope.Sessions.IsCurrentAsync(userId, Stamp(tokens.Token!)));
        }

        [Fact]
        public async Task Token_carries_no_personal_data()
        {
            // El JWT se guarda en el navegador y cualquiera lo decodifica: sin correo, usuario, celular ni foto.
            var (_, tokens) = await SignInAsync("dora");
            var types = Read(tokens.Token!).Claims.Select(c => c.Type).ToHashSet();

            Assert.DoesNotContain(ClaimTypes.Email, types);
            Assert.DoesNotContain(ClaimTypes.UserData, types);
            Assert.DoesNotContain(ClaimTypes.MobilePhone, types);
            Assert.DoesNotContain(ClaimTypes.Surname, types);
            Assert.DoesNotContain(ClaimTypes.Name, types);
            Assert.DoesNotContain(Read(tokens.Token!).Claims, c => c.Value.Contains("dora", StringComparison.OrdinalIgnoreCase));
        }

        [Fact]
        public async Task Logout_revokes_every_token_and_the_refresh_token_immediately()
        {
            var (userId, tokens) = await SignInAsync("beto");

            await using var scope = _identity.Open();
            // Primera lectura queda en caché: el logout debe invalidarla, no esperar a que venza.
            Assert.True(await scope.Sessions.IsCurrentAsync(userId, Stamp(tokens.Token!)));

            var logout = await scope.Tokens.LogoutAsync(userId);
            Assert.True(logout.Succeeded, string.Join(" | ", logout.Messages));

            Assert.False(await scope.Sessions.IsCurrentAsync(userId, Stamp(tokens.Token!)));
            var refresh = await scope.Tokens.GetRefreshTokenAsync(new RefreshTokenRequest { Token = tokens.Token, RefreshToken = tokens.RefreshToken });
            Assert.False(refresh.Succeeded);
        }

        [Fact]
        public async Task Logout_of_unknown_user_fails()
        {
            await using var scope = _identity.Open();
            var logout = await scope.Tokens.LogoutAsync("no-existe");
            Assert.False(logout.Succeeded);
        }

        [Fact]
        public async Task Token_without_stamp_or_with_a_forged_one_is_rejected()
        {
            var (userId, _) = await SignInAsync("carla");

            await using var scope = _identity.Open();
            Assert.False(await scope.Sessions.IsCurrentAsync(userId, null));
            Assert.False(await scope.Sessions.IsCurrentAsync(userId, "00000000000000000000000000000000"));
            Assert.False(await scope.Sessions.IsCurrentAsync("no-existe", "00000000000000000000000000000000"));
        }

        [Fact]
        public async Task Inactive_user_is_rejected_even_with_a_current_stamp()
        {
            var (userId, tokens) = await SignInAsync("dora");

            await using (var scope = _identity.Open())
            {
                var user = await scope.Users.FindByIdAsync(userId);
                user!.IsActive = false;
                await scope.Users.UpdateAsync(user);
            }

            await using var fresh = _identity.Open();
            Assert.False(await fresh.Sessions.IsCurrentAsync(userId, Stamp(tokens.Token!)));
        }

        [Fact]
        public async Task Password_reset_by_identity_revokes_open_tokens()
        {
            var (userId, tokens) = await SignInAsync("eli");

            await using (var scope = _identity.Open())
            {
                var user = await scope.Users.FindByIdAsync(userId);
                var reset = await scope.Users.ChangePasswordAsync(user!, IdentityFactory.Password, "Nueva#2026x");
                Assert.True(reset.Succeeded, string.Join(" | ", reset.Errors.Select(e => e.Description)));
            }

            await using var fresh = _identity.Open();
            Assert.False(await fresh.Sessions.IsCurrentAsync(userId, Stamp(tokens.Token!)));
        }

        [Fact]
        public async Task Mfa_reset_revokes_open_tokens_at_once()
        {
            var (userId, tokens) = await SignInAsync("fito");
            var adminId = await _identity.CreateUserAsync("admin-fito", admin: true);

            await using var scope = _identity.Open(new TestCurrentUserService { UserId = adminId, IsPlatformAdmin = true });
            Assert.True(await scope.Sessions.IsCurrentAsync(userId, Stamp(tokens.Token!)));

            var reset = await scope.Mfa.ResetAsync(userId);
            Assert.True(reset.Succeeded, string.Join(" | ", reset.Messages));
            Assert.False(await scope.Sessions.IsCurrentAsync(userId, Stamp(tokens.Token!)));
        }

        [Fact]
        public async Task New_stamp_with_refresh_token_kept_renews_into_a_current_token()
        {
            // Lo que pasa al cambiar los roles: el token abierto se rechaza, pero el refresh entrega uno nuevo con el stamp actual.
            var (userId, tokens) = await SignInAsync("gina");

            await using var scope = _identity.Open();
            var user = await scope.Users.FindByIdAsync(userId);
            await scope.Users.UpdateSecurityStampAsync(user!);
            scope.Sessions.Invalidate(userId);
            Assert.False(await scope.Sessions.IsCurrentAsync(userId, Stamp(tokens.Token!)));

            var refresh = await scope.Tokens.GetRefreshTokenAsync(new RefreshTokenRequest { Token = tokens.Token, RefreshToken = tokens.RefreshToken });
            Assert.True(refresh.Succeeded, string.Join(" | ", refresh.Messages));
            Assert.True(await scope.Sessions.IsCurrentAsync(userId, Stamp(refresh.Data.Token!)));
            Assert.NotEqual(tokens.RefreshToken, refresh.Data.RefreshToken);
        }
    }
}
