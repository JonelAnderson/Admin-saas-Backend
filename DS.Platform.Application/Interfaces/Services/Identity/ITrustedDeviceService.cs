namespace DS.Platform.Application.Interfaces.Services.Identity
{
    /// <summary>
    /// Dispositivos recordados del login (MFA): el código se pide en un dispositivo nuevo o tras <c>InactivityDays</c> días sin
    /// entrar desde él. Cambiar o restablecer la contraseña, restablecer el MFA o desactivar al usuario los olvida todos.
    /// </summary>
    public interface ITrustedDeviceService
    {
        /// <summary>true si el token es de un dispositivo recordado del usuario usado en los últimos días; renueva su plazo.</summary>
        Task<bool> IsTrustedAsync(string userId, string? deviceToken, CancellationToken cancellationToken = default);

        /// <summary>Recuerda el dispositivo y devuelve su token (se muestra una sola vez).</summary>
        Task<string> TrustAsync(string userId, string? label, CancellationToken cancellationToken = default);

        Task RevokeAllAsync(string userId, CancellationToken cancellationToken = default);
    }
}
