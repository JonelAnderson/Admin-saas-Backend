using DS.Platform.Application.DTOs.Identities.Request;
using DS.Platform.Application.DTOs.Identities.Response;
using DS.Platform.Infrastructure.Services.Identity;
using DS.Platform.Shared.Constants.User;
using DS.Platform.Shared.Wrapper;
using DS.Platform.Tests.Support;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace DS.Platform.Tests.Identity
{
    /// <summary>Fase 12a: login en dos pasos, tickets, bloqueo, códigos de recuperación, refresh y restablecer MFA.</summary>
    public class MfaLoginTests
    {
        private readonly IdentityFactory _identity = new();

        private sealed record Enrolled(string UserId, string SharedKey, TokenResponse Tokens);

        private async Task<Result<TokenResponse>> LoginAsync(string userName, string password = IdentityFactory.Password)
        {
            await using var scope = _identity.Open();
            return await scope.Tokens.LoginAsync(new TokenRequest { UserName = userName, Password = password });
        }

        private async Task<Result<TokenResponse>> MfaAsync(string ticket, string? code = null, string? recoveryCode = null)
        {
            await using var scope = _identity.Open();
            return await scope.Tokens.LoginMfaAsync(new MfaLoginRequest { MfaTicket = ticket, Code = code, RecoveryCode = recoveryCode });
        }

        private async Task<Result<MfaSetupResponse>> SetupAsync(string ticket)
        {
            await using var scope = _identity.Open();
            return await scope.Tokens.BeginMfaSetupAsync(new MfaTicketRequest { MfaTicket = ticket });
        }

        private async Task<Result<TokenResponse>> ConfirmAsync(string ticket, string code)
        {
            await using var scope = _identity.Open();
            return await scope.Tokens.ConfirmMfaSetupAsync(new MfaLoginRequest { MfaTicket = ticket, Code = code });
        }

        private async Task<Enrolled> EnrollAsync(string userName)
        {
            var userId = await _identity.CreateUserAsync(userName);
            var login = await LoginAsync(userName);
            Assert.True(login.Succeeded, string.Join(" | ", login.Messages));
            var setup = await SetupAsync(login.Data.MfaTicket!);
            Assert.True(setup.Succeeded, string.Join(" | ", setup.Messages));
            var confirm = await ConfirmAsync(login.Data.MfaTicket!, Totp.Code(setup.Data.SharedKey));
            Assert.True(confirm.Succeeded, string.Join(" | ", confirm.Messages));
            return new Enrolled(userId, setup.Data.SharedKey, confirm.Data);
        }

        private async Task<string> VerifyTicketAsync(string userName)
        {
            var login = await LoginAsync(userName);
            Assert.True(login.Succeeded && login.Data.MfaRequired, string.Join(" | ", login.Messages));
            return login.Data.MfaTicket!;
        }

        private static bool HasMfaClaim(string? jwt)
            => jwt is not null && new JwtSecurityTokenHandler().ReadJwtToken(jwt).Claims
                .Any(c => c.Type == IdentityService.AmrClaimType && c.Value == IdentityService.AmrMfa);

        [Fact]
        public async Task Correct_password_never_returns_a_token_only_an_mfa_ticket()
        {
            await _identity.CreateUserAsync("ana");

            var first = await LoginAsync("ana");
            Assert.True(first.Succeeded);
            Assert.True(first.Data.MfaSetupRequired);
            Assert.False(first.Data.MfaRequired);
            Assert.False(string.IsNullOrEmpty(first.Data.MfaTicket));
            Assert.Null(first.Data.Token);
            Assert.Null(first.Data.RefreshToken);

            var setup = await SetupAsync(first.Data.MfaTicket!);
            await ConfirmAsync(first.Data.MfaTicket!, Totp.Code(setup.Data.SharedKey));

            var second = await LoginAsync("ana");
            Assert.True(second.Succeeded);
            Assert.True(second.Data.MfaRequired);
            Assert.False(second.Data.MfaSetupRequired);
            Assert.Null(second.Data.Token);
        }

        [Fact]
        public async Task Unknown_user_and_wrong_password_get_the_same_generic_message()
        {
            await _identity.CreateUserAsync("ana");

            var unknown = await LoginAsync("nadie");
            var wrong = await LoginAsync("ana", "Otra$Clave9");

            Assert.False(unknown.Succeeded);
            Assert.False(wrong.Succeeded);
            Assert.Equal(new[] { LoginMessages.InvalidCredentials }, unknown.Messages);
            Assert.Equal(new[] { LoginMessages.InvalidCredentials }, wrong.Messages);
        }

        [Fact]
        public async Task Inactive_user_gets_the_specific_message_only_with_the_right_password()
        {
            var userId = await _identity.CreateUserAsync("ana");
            await using (var scope = _identity.Open())
            {
                var user = await scope.Users.FindByIdAsync(userId);
                user!.IsActive = false;
                await scope.Users.UpdateAsync(user);
            }

            Assert.Equal(new[] { LoginMessages.InvalidCredentials }, (await LoginAsync("ana", "Otra$Clave9")).Messages);
            Assert.Equal(new[] { LoginMessages.UserInactive }, (await LoginAsync("ana")).Messages);
        }

        [Fact]
        public async Task Enrollment_activates_mfa_and_returns_an_amr_mfa_token_with_ten_recovery_codes()
        {
            var userId = await _identity.CreateUserAsync("ana");
            var login = await LoginAsync("ana");

            var setup = await SetupAsync(login.Data.MfaTicket!);
            Assert.True(setup.Succeeded);
            Assert.Matches("^([A-Z2-7]{4} )+[A-Z2-7]{1,4}$", setup.Data.SharedKey);
            var rawKey = setup.Data.SharedKey.Replace(" ", "");
            Assert.Equal($"otpauth://totp/DS.Platform:ana?secret={rawKey}&issuer=DS.Platform&digits=6&period=30", setup.Data.AuthenticatorUri);

            var again = await SetupAsync(login.Data.MfaTicket!);
            Assert.NotEqual(setup.Data.SharedKey, again.Data.SharedKey);
            // La clave anterior quedó sin efecto.
            var oldCode = Totp.Code(setup.Data.SharedKey);
            if (oldCode != Totp.Code(again.Data.SharedKey))
            {
                Assert.False((await ConfirmAsync(login.Data.MfaTicket!, oldCode)).Succeeded);
            }

            var wrong = await ConfirmAsync(login.Data.MfaTicket!, Totp.WrongCode(again.Data.SharedKey));
            Assert.False(wrong.Succeeded);
            Assert.Equal(new[] { LoginMessages.InvalidCode }, wrong.Messages);

            var confirm = await ConfirmAsync(login.Data.MfaTicket!, Totp.Code(again.Data.SharedKey));
            Assert.True(confirm.Succeeded, string.Join(" | ", confirm.Messages));
            Assert.True(HasMfaClaim(confirm.Data.Token));
            Assert.False(string.IsNullOrEmpty(confirm.Data.RefreshToken));
            Assert.Equal(10, confirm.Data.RecoveryCodes!.Count);
            Assert.All(confirm.Data.RecoveryCodes, c => Assert.Matches("^[A-Z0-9]{5}-[A-Z0-9]{5}$", c));
            Assert.Equal(10, confirm.Data.RecoveryCodesLeft);

            await using var scope = _identity.Open();
            var user = await scope.Users.FindByIdAsync(userId);
            Assert.True(user!.TwoFactorEnabled);
            Assert.Equal(0, user.AccessFailedCount);
        }

        [Fact]
        public async Task Login_with_the_app_code_returns_an_amr_mfa_token()
        {
            var ana = await EnrollAsync("ana");
            var ticket = await VerifyTicketAsync("ana");

            var result = await MfaAsync(ticket, Totp.Code(ana.SharedKey));

            Assert.True(result.Succeeded, string.Join(" | ", result.Messages));
            Assert.True(HasMfaClaim(result.Data.Token));
            Assert.Equal(10, result.Data.RecoveryCodesLeft);
            Assert.False(string.IsNullOrEmpty(result.Data.RefreshToken));
        }

        [Fact]
        public async Task Expired_tampered_foreign_wrong_stage_and_stale_tickets_are_rejected()
        {
            var ana = await EnrollAsync("ana");
            var luis = await EnrollAsync("luis");
            var code = Totp.Code(ana.SharedKey);

            string anaStamp;
            await using (var scope = _identity.Open())
            {
                anaStamp = (await scope.Users.FindByIdAsync(ana.UserId))!.SecurityStamp!;
                var luisStamp = (await scope.Users.FindByIdAsync(luis.UserId))!.SecurityStamp!;

                var expired = scope.Tickets.Create(ana.UserId, anaStamp, MfaTicketProtector.StageVerify, DateTimeOffset.UtcNow.AddSeconds(-1));
                Assert.Equal(new[] { LoginMessages.TicketExpired }, (await MfaAsync(expired, code)).Messages);

                // Id de un usuario con el stamp de otro.
                var foreign = scope.Tickets.Create(ana.UserId, luisStamp, MfaTicketProtector.StageVerify);
                Assert.Equal(new[] { LoginMessages.TicketExpired }, (await MfaAsync(foreign, code)).Messages);

                // Ticket de enrolamiento usado para verificar, y al revés.
                var setupTicket = scope.Tickets.Create(ana.UserId, anaStamp, MfaTicketProtector.StageSetup);
                Assert.Equal(new[] { LoginMessages.TicketExpired }, (await MfaAsync(setupTicket, code)).Messages);
            }

            var ticket = await VerifyTicketAsync("ana");
            Assert.False((await SetupAsync(ticket)).Succeeded);

            var tampered = ticket[..^4] + (ticket[^4] == 'A' ? "B" : "A") + ticket[^3..];
            Assert.Equal(new[] { LoginMessages.TicketExpired }, (await MfaAsync(tampered, code)).Messages);

            // Otro anillo de claves (otro servidor / clave perdida).
            var otherRing = new MfaTicketProtector(new Microsoft.AspNetCore.DataProtection.EphemeralDataProtectionProvider())
                .Create(ana.UserId, anaStamp, MfaTicketProtector.StageVerify);
            Assert.Equal(new[] { LoginMessages.TicketExpired }, (await MfaAsync(otherRing, code)).Messages);

            // El código de Luis no sirve con el ticket de Ana.
            var luisCode = Totp.Code(luis.SharedKey);
            if (luisCode != code)
            {
                Assert.Equal(new[] { LoginMessages.InvalidCode }, (await MfaAsync(ticket, luisCode)).Messages);
            }

            // Cambiar la contraseña cambia el security stamp: el ticket deja de servir.
            await using (var scope = _identity.Open())
            {
                var user = await scope.Users.FindByIdAsync(ana.UserId);
                var changed = await scope.Users.ChangePasswordAsync(user!, IdentityFactory.Password, "Nueva$Clave2026");
                Assert.True(changed.Succeeded);
            }
            Assert.Equal(new[] { LoginMessages.TicketExpired }, (await MfaAsync(ticket, Totp.Code(ana.SharedKey))).Messages);
        }

        [Fact]
        public void The_ticket_is_not_a_jwt_and_cannot_be_used_as_bearer()
        {
            var ticket = new MfaTicketProtector(_identity.DataProtection).Create("user-1", "stamp", MfaTicketProtector.StageVerify);
            var handler = new JwtSecurityTokenHandler();

            Assert.False(handler.CanReadToken(ticket));
            Assert.ThrowsAny<Exception>(() => handler.ValidateToken(ticket, new TokenValidationParameters
            {
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(IdentityFactory.JwtSecret)),
                ValidateIssuer = false,
                ValidateAudience = false
            }, out _));
        }

        [Fact]
        public async Task Five_wrong_passwords_lock_the_account_for_15_minutes_and_admin_unlocks_it()
        {
            var userId = await _identity.CreateUserAsync("ana");

            for (var i = 1; i <= 4; i++)
            {
                Assert.Equal(new[] { LoginMessages.InvalidCredentials }, (await LoginAsync("ana", "Mala$Clave1")).Messages);
            }
            var fifth = await LoginAsync("ana", "Mala$Clave1");
            Assert.StartsWith("Cuenta bloqueada por intentos fallidos hasta las ", fifth.Messages.Single());

            // Bloqueada: ni la contraseña correcta entra.
            var locked = await LoginAsync("ana");
            Assert.False(locked.Succeeded);
            Assert.StartsWith("Cuenta bloqueada", locked.Messages.Single());

            await using (var scope = _identity.Open())
            {
                var user = await scope.Users.FindByIdAsync(userId);
                Assert.InRange(user!.LockoutEnd!.Value - DateTimeOffset.UtcNow, TimeSpan.FromMinutes(14), TimeSpan.FromMinutes(15.1));
            }

            await using (var admin = _identity.Open(new TestCurrentUserService { UserId = "admin-1", IsPlatformAdmin = true }))
            {
                Assert.True((await admin.Mfa.UnlockAsync(userId)).Succeeded);
            }

            var after = await LoginAsync("ana");
            Assert.True(after.Succeeded);
            Assert.True(after.Data.MfaSetupRequired);
        }

        [Fact]
        public async Task Wrong_mfa_codes_count_with_wrong_passwords_and_a_full_login_resets_the_counter()
        {
            var ana = await EnrollAsync("ana");

            for (var i = 0; i < 3; i++)
            {
                await LoginAsync("ana", "Mala$Clave1");
            }
            var ticket = await VerifyTicketAsync("ana");
            Assert.Equal(new[] { LoginMessages.InvalidCode }, (await MfaAsync(ticket, Totp.WrongCode(ana.SharedKey))).Messages);
            var fifth = await MfaAsync(ticket, Totp.WrongCode(ana.SharedKey));
            Assert.StartsWith("Cuenta bloqueada", fifth.Messages.Single());
            Assert.StartsWith("Cuenta bloqueada", (await MfaAsync(ticket, Totp.Code(ana.SharedKey))).Messages.Single());

            await using (var admin = _identity.Open(new TestCurrentUserService { UserId = "admin-1", IsPlatformAdmin = true }))
            {
                await admin.Mfa.UnlockAsync(ana.UserId);
            }

            await LoginAsync("ana", "Mala$Clave1");
            var ok = await MfaAsync(await VerifyTicketAsync("ana"), Totp.Code(ana.SharedKey));
            Assert.True(ok.Succeeded);

            await using var check = _identity.Open();
            Assert.Equal(0, (await check.Users.FindByIdAsync(ana.UserId))!.AccessFailedCount);
        }

        [Fact]
        public async Task Recovery_code_works_once_and_codes_are_stored_hashed_and_the_key_encrypted()
        {
            var ana = await EnrollAsync("ana");
            var recovery = ana.Tokens.RecoveryCodes![0];

            var first = await MfaAsync(await VerifyTicketAsync("ana"), recoveryCode: recovery.ToLowerInvariant());
            Assert.True(first.Succeeded, string.Join(" | ", first.Messages));
            Assert.True(HasMfaClaim(first.Data.Token));
            Assert.Equal(9, first.Data.RecoveryCodesLeft);

            var second = await MfaAsync(await VerifyTicketAsync("ana"), recoveryCode: recovery);
            Assert.False(second.Succeeded);
            Assert.Equal(new[] { LoginMessages.InvalidCode }, second.Messages);

            await using var scope = _identity.Open();
            var tokens = scope.Db.UserTokens.Where(t => t.UserId == ana.UserId).ToList();
            var codes = tokens.Single(t => t.Name == AdmUserStore.RecoveryCodeTokenName).Value!;
            foreach (var code in ana.Tokens.RecoveryCodes)
            {
                Assert.DoesNotContain(code, codes);
            }
            Assert.Equal(9, codes.Split(';').Length);
            Assert.Contains(AdmUserStore.HashRecoveryCode(ana.Tokens.RecoveryCodes[1]), codes);

            var storedKey = tokens.Single(t => t.Name == AdmUserStore.AuthenticatorKeyTokenName).Value!;
            var rawKey = ana.SharedKey.Replace(" ", "");
            Assert.NotEqual(rawKey, storedKey);
            Assert.DoesNotContain(rawKey, storedKey);
            Assert.Equal(rawKey, _identity.DataProtection.CreateProtector(AdmUserStore.AuthenticatorKeyPurpose).Unprotect(storedKey));
            Assert.Equal(rawKey, await scope.Users.GetAuthenticatorKeyAsync((await scope.Users.FindByIdAsync(ana.UserId))!));
        }

        // ------------------------------------------------------------------ dispositivo recordado

        private async Task<Result<TokenResponse>> LoginFromAsync(string userName, string? deviceToken)
        {
            await using var scope = _identity.Open();
            return await scope.Tokens.LoginAsync(new TokenRequest { UserName = userName, Password = IdentityFactory.Password, DeviceToken = deviceToken });
        }

        /// <summary>Login + código (de recuperación, para no repetir el TOTP de la misma ventana) recordando el dispositivo.</summary>
        private async Task<string> RememberDeviceAsync(Enrolled user, string userName)
        {
            var ticket = await VerifyTicketAsync(userName);
            await using var scope = _identity.Open();
            var verified = await scope.Tokens.LoginMfaAsync(new MfaLoginRequest
            {
                MfaTicket = ticket, RecoveryCode = user.Tokens.RecoveryCodes![0], RememberDevice = true, DeviceLabel = "Chrome en Windows"
            });
            Assert.True(verified.Succeeded, string.Join(" | ", verified.Messages));
            Assert.False(string.IsNullOrEmpty(verified.Data.DeviceToken));
            return verified.Data.DeviceToken!;
        }

        [Fact]
        public async Task A_remembered_device_logs_in_without_the_code_and_keeps_the_mfa_claim()
        {
            var ana = await EnrollAsync("ana");
            var device = await RememberDeviceAsync(ana, "ana");

            var login = await LoginFromAsync("ana", device);
            Assert.True(login.Succeeded && login.Data.DeviceTrusted, string.Join(" | ", login.Messages));
            Assert.False(login.Data.MfaRequired);
            Assert.True(HasMfaClaim(login.Data.Token));
            // La contraseña sigue siendo obligatoria.
            await using (var scope = _identity.Open())
                Assert.Null((await scope.Tokens.LoginAsync(new TokenRequest { UserName = "ana", Password = "Mala$Clave1", DeviceToken = device })).Data?.Token);
            // Otro dispositivo (o un token inventado) pide el código.
            Assert.True((await LoginFromAsync("ana", null)).Data.MfaRequired);
            Assert.True((await LoginFromAsync("ana", "inventado")).Data.MfaRequired);
        }

        [Fact]
        public async Task The_code_is_asked_again_only_after_7_days_without_logging_in_from_that_device()
        {
            var ana = await EnrollAsync("ana");
            var device = await RememberDeviceAsync(ana, "ana");

            // Entró hace 6 días: no pide código y el plazo vuelve a empezar.
            await using (var scope = _identity.Open())
            {
                scope.Db.TrustedDevices.Single().LastUsedOnUtc = DateTime.UtcNow.AddDays(-6);
                await scope.Db.SaveChangesAsync();
            }
            Assert.True((await LoginFromAsync("ana", device)).Data.DeviceTrusted);
            await using (var scope = _identity.Open())
                Assert.True(scope.Db.TrustedDevices.AsNoTracking().Single().LastUsedOnUtc > DateTime.UtcNow.AddMinutes(-1));

            // 7 días y un poco sin entrar desde ese dispositivo: vuelve a pedir el código.
            await using (var scope = _identity.Open())
            {
                scope.Db.TrustedDevices.Single().LastUsedOnUtc = DateTime.UtcNow.AddDays(-7).AddMinutes(-5);
                await scope.Db.SaveChangesAsync();
            }
            var stale = await LoginFromAsync("ana", device);
            Assert.True(stale.Data.MfaRequired && !stale.Data.DeviceTrusted);
        }

        [Fact]
        public async Task Another_users_device_token_does_not_skip_the_code()
        {
            var ana = await EnrollAsync("ana");
            var device = await RememberDeviceAsync(ana, "ana");
            await EnrollAsync("beto");

            Assert.True((await LoginFromAsync("beto", device)).Data.MfaRequired);
        }

        [Fact]
        public async Task Resetting_the_mfa_forgets_the_remembered_devices()
        {
            var ana = await EnrollAsync("ana");
            var device = await RememberDeviceAsync(ana, "ana");
            await using (var admin = _identity.Open(new TestCurrentUserService { UserId = "admin-1", IsPlatformAdmin = true }))
                Assert.True((await admin.Mfa.ResetAsync(ana.UserId)).Succeeded);

            var login = await LoginFromAsync("ana", device);
            Assert.False(login.Data.DeviceTrusted);
            Assert.True(login.Data.MfaSetupRequired);
            await using (var scope = _identity.Open())
                Assert.Empty(scope.Db.TrustedDevices.AsNoTracking());
        }

        [Fact]
        public async Task Failed_logins_by_someone_else_do_not_cut_an_open_session()
        {
            // Auditoría run-1: un anónimo que conoce el usuario lo bloquea con 5 contraseñas malas; la sesión abierta sigue renovándose.
            var ana = await EnrollAsync("ana");
            for (var i = 1; i <= 5; i++)
                await LoginAsync("ana", "Mala$Clave1");
            Assert.StartsWith("Cuenta bloqueada", (await LoginAsync("ana")).Messages.Single());

            await using var scope = _identity.Open();
            var refreshed = await scope.Tokens.GetRefreshTokenAsync(new RefreshTokenRequest { Token = ana.Tokens.Token, RefreshToken = ana.Tokens.RefreshToken });
            Assert.True(refreshed.Succeeded, "Pista confirmada: el bloqueo provocado por otro cortó la renovación de la sesión de ana.");
        }

        [Fact]
        public async Task Refresh_requires_an_amr_mfa_token()
        {
            var ana = await EnrollAsync("ana");

            await using (var scope = _identity.Open())
            {
                var refreshed = await scope.Tokens.GetRefreshTokenAsync(new RefreshTokenRequest { Token = ana.Tokens.Token, RefreshToken = ana.Tokens.RefreshToken });
                Assert.True(refreshed.Succeeded, string.Join(" | ", refreshed.Messages));
                Assert.True(HasMfaClaim(refreshed.Data.Token));

                // Un token emitido sin MFA (como los de antes de la Fase 12a), con la firma válida.
                var legacy = new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(
                    issuer: IdentityFactory.JwtIssuer, audience: IdentityFactory.JwtIssuer,
                    claims: new[] { new Claim(ClaimTypes.NameIdentifier, ana.UserId) },
                    expires: DateTime.UtcNow.AddHours(1),
                    signingCredentials: new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(IdentityFactory.JwtSecret)), SecurityAlgorithms.HmacSha256)));
                var rejected = await scope.Tokens.GetRefreshTokenAsync(new RefreshTokenRequest { Token = legacy, RefreshToken = refreshed.Data.RefreshToken });
                Assert.False(rejected.Succeeded);
                Assert.Equal(new[] { LoginMessages.SessionExpired }, rejected.Messages);

                Assert.False((await scope.Tokens.GetRefreshTokenAsync(new RefreshTokenRequest { Token = "basura", RefreshToken = "x" })).Succeeded);
            }
        }

        [Fact]
        public async Task Reset_mfa_invalidates_the_refresh_token_and_forces_a_new_enrollment()
        {
            var ana = await EnrollAsync("ana");
            var pendingTicket = await VerifyTicketAsync("ana");

            await using (var admin = _identity.Open(new TestCurrentUserService { UserId = "admin-1", IsPlatformAdmin = true }))
            {
                var reset = await admin.Mfa.ResetAsync(ana.UserId);
                Assert.True(reset.Succeeded, string.Join(" | ", reset.Messages));
            }

            await using (var scope = _identity.Open())
            {
                var user = await scope.Users.FindByIdAsync(ana.UserId);
                Assert.False(user!.TwoFactorEnabled);
                Assert.Null(user.RefreshToken);
                Assert.Empty(scope.Db.UserTokens.Where(t => t.UserId == ana.UserId));

                var refresh = await scope.Tokens.GetRefreshTokenAsync(new RefreshTokenRequest { Token = ana.Tokens.Token, RefreshToken = ana.Tokens.RefreshToken });
                Assert.False(refresh.Succeeded);
                var nullRefresh = await scope.Tokens.GetRefreshTokenAsync(new RefreshTokenRequest { Token = ana.Tokens.Token, RefreshToken = null });
                Assert.False(nullRefresh.Succeeded);
            }

            // El ticket pedido antes del restablecimiento ya no sirve (nuevo security stamp).
            Assert.Equal(new[] { LoginMessages.TicketExpired }, (await MfaAsync(pendingTicket, Totp.Code(ana.SharedKey))).Messages);

            var login = await LoginAsync("ana");
            Assert.True(login.Data.MfaSetupRequired);
            var setup = await SetupAsync(login.Data.MfaTicket!);
            var confirm = await ConfirmAsync(login.Data.MfaTicket!, Totp.Code(setup.Data.SharedKey));
            Assert.True(confirm.Succeeded);
        }

        [Fact]
        public async Task An_admin_cannot_reset_their_own_mfa()
        {
            var ana = await EnrollAsync("ana");

            await using var self = _identity.Open(new TestCurrentUserService { UserId = ana.UserId });
            var result = await self.Mfa.ResetAsync(ana.UserId);

            Assert.False(result.Succeeded);
            Assert.Equal(new[] { LoginMessages.CannotResetOwnMfa }, result.Messages);
            Assert.True((await self.Users.FindByIdAsync(ana.UserId))!.TwoFactorEnabled);
        }

        [Fact]
        public async Task Regenerating_recovery_codes_needs_the_current_app_code()
        {
            var ana = await EnrollAsync("ana");
            await using var scope = _identity.Open(new TestCurrentUserService { UserId = ana.UserId });

            Assert.False((await scope.Mfa.RegenerateRecoveryCodesAsync(ana.UserId, Totp.WrongCode(ana.SharedKey))).Succeeded);
            var regenerated = await scope.Mfa.RegenerateRecoveryCodesAsync(ana.UserId, Totp.Code(ana.SharedKey));
            Assert.True(regenerated.Succeeded);
            Assert.Equal(10, regenerated.Data.Count);
            Assert.Empty(regenerated.Data.Intersect(ana.Tokens.RecoveryCodes!));

            var status = await scope.Mfa.GetStatusAsync(ana.UserId);
            Assert.True(status.Data.Enabled);
            Assert.Equal(10, status.Data.RecoveryCodesLeft);

            Assert.False((await MfaAsync(await VerifyTicketAsync("ana"), recoveryCode: ana.Tokens.RecoveryCodes![0])).Succeeded);
        }
    }
}
