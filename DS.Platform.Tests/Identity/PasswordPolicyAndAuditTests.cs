using DS.Platform.Application.DTOs.Identities.Request;
using DS.Platform.Application.Validators.Requests.Identity;
using DS.Platform.Domain.Entities.Identity;
using DS.Platform.Infrastructure.Contexts;
using DS.Platform.Infrastructure.Services.Identity;
using DS.Platform.Tests.Support;
using Microsoft.EntityFrameworkCore;

namespace DS.Platform.Tests.Identity
{
    /// <summary>Fase 12a: política de contraseña 8–16 y enmascarado de secretos en la auditoría.</summary>
    public class PasswordPolicyAndAuditTests
    {
        private readonly IdentityFactory _identity = new();

        private async Task<(bool Ok, string[] Codes)> CreateAsync(string password)
        {
            await using var scope = _identity.Open();
            var name = "u" + Guid.NewGuid().ToString("N")[..8];
            var result = await scope.Users.CreateAsync(new AdmUser
            {
                UserName = name,
                Email = $"{name}@tests.local",
                EmailConfirmed = true,
                IsActive = true,
                CreatedBy = "tests"
            }, password);
            return (result.Succeeded, result.Errors.Select(e => e.Code).ToArray());
        }

        [Theory]
        [InlineData("Ab1$xyz", "PasswordTooShort")]            // 7
        [InlineData("Ab1$xyzwvutsrqpo", null)]                // 16
        [InlineData("Ab1$xyzwvutsrqpon", "PasswordTooLong")]   // 17
        [InlineData("ab1$xyzw", "PasswordRequiresUpper")]
        [InlineData("AB1$XYZW", "PasswordRequiresLower")]
        [InlineData("Abc$xyzw", "PasswordRequiresDigit")]
        [InlineData("Ab12xyzw", "PasswordRequiresNonAlphanumeric")]
        [InlineData("Ab1$xyzw", null)]                         // 8
        public async Task Identity_enforces_8_to_16_with_upper_lower_digit_and_special(string password, string? expectedError)
        {
            var (ok, codes) = await CreateAsync(password);

            if (expectedError is null)
            {
                Assert.True(ok, string.Join(", ", codes));
            }
            else
            {
                Assert.False(ok);
                Assert.Contains(expectedError, codes);
            }
        }

        [Fact]
        public async Task Password_errors_are_in_spanish()
        {
            await using var scope = _identity.Open();
            var result = await scope.Users.CreateAsync(new AdmUser { UserName = "corto", Email = "corto@tests.local", CreatedBy = "t" }, "Ab1$xyzwvutsrqpon");

            Assert.Contains(result.Errors, e => e.Description == "La contraseña debe tener como máximo 16 caracteres.");
        }

        [Theory]
        [InlineData("Ab1$xyz", false)]
        [InlineData("Ab1$xyzwvutsrqpon", false)]
        [InlineData("ab1$xyzw", false)]
        [InlineData("AB1$XYZW", false)]
        [InlineData("Abc$xyzw", false)]
        [InlineData("Ab12xyzw", false)]
        [InlineData("Ab1~xyzw", true)]
        public void Request_validators_apply_the_same_policy(string password, bool valid)
        {
            var register = new RegisterRequestValidator().Validate(new RegisterRequest
            {
                FirstName = "Nombre", LastName = "Apellido", UserName = "usuario", Email = "u@tests.local",
                Password = password, ConfirmPassword = password
            });
            var change = new ChangePasswordRequestValidator().Validate(new ChangePasswordRequest
            {
                Password = "Actual$123", NewPassword = password, ConfirmNewPassword = password
            });

            Assert.Equal(valid, register.IsValid);
            Assert.Equal(valid, change.IsValid);
        }

        [Fact]
        public async Task Audit_masks_password_hash_stamps_refresh_token_and_authenticator_key()
        {
            var userId = await _identity.CreateUserAsync("ana");
            string hashBefore;

            await using (var scope = _identity.Open(new TestCurrentUserService { UserId = "admin-1" }))
            {
                var user = (await scope.Users.FindByIdAsync(userId))!;
                hashBefore = user.PasswordHash!;
                Assert.True((await scope.Users.ChangePasswordAsync(user, IdentityFactory.Password, "Nueva$Clave2026")).Succeeded);
                user.RefreshToken = "refresh-token-en-claro";
                await scope.Users.UpdateAsync(user);
                user.RefreshToken = "otro-refresh-token";
                await scope.Users.UpdateAsync(user);
                await scope.Mfa.CreateAuthenticatorKeyAsync(user);
                await scope.Mfa.CreateAuthenticatorKeyAsync(user);
            }

            await using var verify = _identity.Open();
            string hashAfter = (await verify.Users.FindByIdAsync(userId))!.PasswordHash!;
            var key = await verify.Users.GetAuthenticatorKeyAsync((await verify.Users.FindByIdAsync(userId))!);
            var storedKey = verify.Db.UserTokens.Single(t => t.UserId == userId && t.Name == AdmUserStore.AuthenticatorKeyTokenName).Value!;
            var audits = await verify.Db.AuditTrails.AsNoTracking().ToListAsync();

            var userAudits = audits.Where(a => a.TableName == nameof(AdmUser)).ToList();
            Assert.Contains(userAudits, a => a.AffectedColumns != null && a.AffectedColumns.Contains("PasswordHash")
                && a.NewValues!.Contains("\"PasswordHash\":\"***\"") && a.OldValues!.Contains("\"PasswordHash\":\"***\""));
            Assert.Contains(userAudits, a => a.NewValues != null && a.NewValues.Contains("\"RefreshToken\":\"***\""));
            Assert.Contains(userAudits, a => a.NewValues != null && a.NewValues.Contains("\"SecurityStamp\":\"***\""));

            var tokenAudits = audits.Where(a => a.TableName == "IdentityUserToken`1").ToList();
            Assert.Contains(tokenAudits, a => a.Type == "Create" && a.NewValues!.Contains("\"Value\":\"***\""));
            Assert.Contains(tokenAudits, a => a.Type == "Update" && a.OldValues!.Contains("\"Value\":\"***\""));

            foreach (var audit in audits)
            {
                var text = audit.OldValues + audit.NewValues;
                Assert.DoesNotContain(hashBefore, text);
                Assert.DoesNotContain(hashAfter, text);
                Assert.DoesNotContain("refresh-token-en-claro", text);
                Assert.DoesNotContain("otro-refresh-token", text);
                Assert.DoesNotContain(key!, text);
                Assert.DoesNotContain(storedKey, text);
            }
        }

        [Fact]
        public void Only_user_secrets_and_token_values_are_sensitive()
        {
            Assert.True(AuditableContext.IsSensitiveColumn(new AdmUser(), nameof(AdmUser.PasswordHash)));
            Assert.True(AuditableContext.IsSensitiveColumn(new AdmUser(), nameof(AdmUser.RefreshToken)));
            Assert.False(AuditableContext.IsSensitiveColumn(new AdmUser(), nameof(AdmUser.UserName)));
            Assert.True(AuditableContext.IsSensitiveColumn(new Microsoft.AspNetCore.Identity.IdentityUserToken<string>(), "Value"));
            Assert.False(AuditableContext.IsSensitiveColumn(new Microsoft.AspNetCore.Identity.IdentityUserToken<string>(), "Name"));
            Assert.False(AuditableContext.IsSensitiveColumn(new object(), "Value"));
        }
    }
}
