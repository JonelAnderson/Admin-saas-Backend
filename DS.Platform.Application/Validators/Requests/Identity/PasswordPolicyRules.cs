using FluentValidation;

namespace DS.Platform.Application.Validators.Requests.Identity
{
    /// <summary>
    /// Política de contraseña (Fase 12a): 8 a 16 caracteres con mayúscula, minúscula, número y carácter especial.
    /// Identity la vuelve a exigir al guardar (<c>options.Password</c> + <c>ErpPasswordValidator</c>).
    /// </summary>
    public static class PasswordPolicyRules
    {
        public const int MinLength = 8;
        public const int MaxLength = 16;

        public static IRuleBuilderOptions<T, string?> ErpPassword<T>(this IRuleBuilder<T, string?> rule)
            => rule
                .Must(x => !string.IsNullOrWhiteSpace(x)).WithMessage("La contraseña es obligatoria.")
                .Must(x => x is null || (x.Length >= MinLength && x.Length <= MaxLength))
                    .WithMessage($"La contraseña debe tener entre {MinLength} y {MaxLength} caracteres.")
                .Matches(@"[A-Z]").WithMessage("La contraseña debe tener al menos una letra mayúscula.")
                .Matches(@"[a-z]").WithMessage("La contraseña debe tener al menos una letra minúscula.")
                .Matches(@"[0-9]").WithMessage("La contraseña debe tener al menos un número.")
                .Matches(@"[^a-zA-Z0-9]").WithMessage("La contraseña debe tener al menos un carácter especial.");
    }
}
