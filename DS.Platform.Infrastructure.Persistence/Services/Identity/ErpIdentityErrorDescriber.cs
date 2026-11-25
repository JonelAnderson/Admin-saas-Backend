using Microsoft.AspNetCore.Identity;

namespace DS.Platform.Infrastructure.Services.Identity
{
    /// <summary>Mensajes de Identity en español.</summary>
    public class ErpIdentityErrorDescriber : IdentityErrorDescriber
    {
        public override IdentityError DefaultError() => E(nameof(DefaultError), "Ocurrió un error.");
        public override IdentityError ConcurrencyFailure() => E(nameof(ConcurrencyFailure), "Otro usuario modificó el registro; vuelva a intentarlo.");
        public override IdentityError PasswordMismatch() => E(nameof(PasswordMismatch), "La contraseña actual es incorrecta.");
        public override IdentityError InvalidToken() => E(nameof(InvalidToken), "El enlace o código no es válido o ya venció.");
        public override IdentityError RecoveryCodeRedemptionFailed() => E(nameof(RecoveryCodeRedemptionFailed), "Código de recuperación incorrecto.");
        public override IdentityError LoginAlreadyAssociated() => E(nameof(LoginAlreadyAssociated), "Ya existe un usuario con este inicio de sesión.");
        public override IdentityError InvalidUserName(string? userName) => E(nameof(InvalidUserName), $"El usuario '{userName}' no es válido: use solo letras, números y - . _ @ +.");
        public override IdentityError InvalidEmail(string? email) => E(nameof(InvalidEmail), $"El correo '{email}' no es válido.");
        public override IdentityError DuplicateUserName(string userName) => E(nameof(DuplicateUserName), $"El usuario '{userName}' ya existe.");
        public override IdentityError DuplicateEmail(string email) => E(nameof(DuplicateEmail), $"El correo '{email}' ya está registrado.");
        public override IdentityError InvalidRoleName(string? role) => E(nameof(InvalidRoleName), $"El rol '{role}' no es válido.");
        public override IdentityError DuplicateRoleName(string role) => E(nameof(DuplicateRoleName), $"El rol '{role}' ya existe.");
        public override IdentityError UserAlreadyHasPassword() => E(nameof(UserAlreadyHasPassword), "El usuario ya tiene contraseña.");
        public override IdentityError UserLockoutNotEnabled() => E(nameof(UserLockoutNotEnabled), "El bloqueo por intentos no está habilitado para este usuario.");
        public override IdentityError UserAlreadyInRole(string role) => E(nameof(UserAlreadyInRole), $"El usuario ya tiene el rol '{role}'.");
        public override IdentityError UserNotInRole(string role) => E(nameof(UserNotInRole), $"El usuario no tiene el rol '{role}'.");
        public override IdentityError PasswordTooShort(int length) => E(nameof(PasswordTooShort), $"La contraseña debe tener al menos {length} caracteres.");
        public override IdentityError PasswordRequiresUniqueChars(int uniqueChars) => E(nameof(PasswordRequiresUniqueChars), $"La contraseña debe tener al menos {uniqueChars} caracteres distintos.");
        public override IdentityError PasswordRequiresNonAlphanumeric() => E(nameof(PasswordRequiresNonAlphanumeric), "La contraseña debe tener al menos un carácter especial.");
        public override IdentityError PasswordRequiresDigit() => E(nameof(PasswordRequiresDigit), "La contraseña debe tener al menos un número.");
        public override IdentityError PasswordRequiresLower() => E(nameof(PasswordRequiresLower), "La contraseña debe tener al menos una letra minúscula.");
        public override IdentityError PasswordRequiresUpper() => E(nameof(PasswordRequiresUpper), "La contraseña debe tener al menos una letra mayúscula.");

        private static IdentityError E(string code, string description) => new() { Code = code, Description = description };
    }
}
