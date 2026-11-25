using System;

namespace DS.Platform.Shared.Constants.User
{
    /// <summary>
    /// Mensajes del login y del MFA (Fase 12a). El de credenciales es único para no revelar si el usuario existe.
    /// </summary>
    public static class LoginMessages
    {
        public const string InvalidCredentials = "Usuario o contraseña incorrectos.";
        public const string InvalidCode = "Código incorrecto.";
        public const string TicketExpired = "La sesión de verificación venció; vuelva a iniciar sesión.";
        public const string SessionExpired = "La sesión venció; vuelva a iniciar sesión.";
        public const string UserInactive = "El usuario está inactivo. Comuníquese con el administrador.";
        public const string EmailNotConfirmed = "Debe confirmar su correo electrónico antes de ingresar.";
        public const string CodeRequired = "Ingrese el código de la app autenticadora o un código de recuperación.";
        public const string SetupFirst = "Primero genere la clave de la app autenticadora.";
        public const string MfaNotEnabled = "La autenticación en dos pasos no está activada.";
        public const string CannotResetOwnMfa = "No puede restablecer su propia autenticación en dos pasos; pídalo a otro administrador.";
        public const string UserNotFound = "Usuario no encontrado.";
        public const string MfaReset = "Autenticación en dos pasos restablecida: el usuario se enrolará en su siguiente inicio de sesión.";
        public const string Unlocked = "Cuenta desbloqueada.";
        public const string LoginOk = "Inicio de sesión correcto.";

        /// <summary>Hora de Lima (UTC-5, sin horario de verano), como el resto del sistema.</summary>
        public static string LockedUntil(DateTimeOffset lockoutEnd)
            => $"Cuenta bloqueada por intentos fallidos hasta las {lockoutEnd.UtcDateTime.AddHours(-5):HH:mm}.";
    }
}
