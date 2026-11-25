using System;

namespace DS.Platform.Domain.Entities.Identity
{
    /// <summary>
    /// Dispositivo recordado tras un login con MFA: desde él, el login pide solo usuario y contraseña mientras se use al menos una
    /// vez cada 7 días. Se guarda el SHA-256 del token, nunca el token.
    /// </summary>
    public class TrustedDevice
    {
        public long Id { get; set; }
        public required string UserId { get; set; }
        public required string TokenHash { get; set; }
        /// <summary>Navegador / sistema, para reconocerlo (no es un dato de seguridad).</summary>
        public string? Label { get; set; }
        public DateTime CreatedOnUtc { get; set; }
        /// <summary>Último login desde el dispositivo: el plazo de inactividad se cuenta desde aquí.</summary>
        public DateTime LastUsedOnUtc { get; set; }
    }
}
