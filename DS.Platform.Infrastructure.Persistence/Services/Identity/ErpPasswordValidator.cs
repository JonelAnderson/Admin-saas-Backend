using DS.Platform.Domain.Entities.Identity;
using Microsoft.AspNetCore.Identity;

namespace DS.Platform.Infrastructure.Services.Identity
{
    /// <summary>
    /// Máximo de 16 caracteres (Identity no trae máximo). El mínimo de 8, mayúscula, minúscula, número y carácter
    /// especial los exige <c>options.Password</c> (<see cref="ErpIdentitySetup.Configure"/>).
    /// </summary>
    public class ErpPasswordValidator : IPasswordValidator<AdmUser>
    {
        public const int MaxLength = 16;

        public Task<IdentityResult> ValidateAsync(UserManager<AdmUser> manager, AdmUser user, string? password)
            => Task.FromResult(password is not null && password.Length > MaxLength
                ? IdentityResult.Failed(new IdentityError
                {
                    Code = "PasswordTooLong",
                    Description = $"La contraseña debe tener como máximo {MaxLength} caracteres."
                })
                : IdentityResult.Success);
    }
}
