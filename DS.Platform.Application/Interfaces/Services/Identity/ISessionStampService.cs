namespace DS.Platform.Application.Interfaces.Services.Identity
{
    /// <summary>
    /// Revocación de sesiones: cada JWT lleva el sello de sesión del usuario (huella de su security stamp) y el servidor lo compara
    /// en cada request. Cerrar sesión, cambiar o restablecer la contraseña, restablecer el MFA, cambiar los roles o un cierre
    /// forzado cambian el security stamp: desde ese momento los tokens anteriores del usuario (en todos sus dispositivos) se
    /// rechazan. También se rechazan los de un usuario desactivado o borrado.
    /// </summary>
    public interface ISessionStampService
    {
        /// <summary>El sello del token es el vigente y el usuario sigue activo (con caché corta para no consultar la base en cada request).</summary>
        Task<bool> IsCurrentAsync(string userId, string? tokenStamp, CancellationToken cancellationToken = default);

        /// <summary>Olvida lo guardado en caché del usuario (después de cambiar su security stamp en esta instancia).</summary>
        void Invalidate(string userId);
    }
}
