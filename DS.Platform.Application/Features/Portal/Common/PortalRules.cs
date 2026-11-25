namespace DS.Platform.Application.Features.Portal.Common
{
    /// <summary>Reglas puras del acceso al portal de clientes (fase C6).</summary>
    public static class PortalRules
    {
        public const string NotConfiguredMessage = "El portal no tiene dirección pública https (Portal:PublicUrl): las invitaciones esperan.";

        /// <summary>El correo es el usuario del portal: sin espacios y en minúsculas.</summary>
        public static string NormalizeEmail(string email) => (email ?? string.Empty).Trim().ToLowerInvariant();

        /// <summary>Enlace de activación: <c>{PublicUrl}/activar?token=...</c> (el token va codificado para URL).</summary>
        public static string ActivationUrl(string publicUrl, string token)
            => $"{publicUrl.Trim().TrimEnd('/')}/activar?token={Uri.EscapeDataString(token)}";
    }
}
