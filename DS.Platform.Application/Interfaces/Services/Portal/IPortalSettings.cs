namespace DS.Platform.Application.Interfaces.Services.Portal
{
    /// <summary>
    /// Configuración del portal de clientes de la API SUNAT (sección <c>Portal</c>; nada secreto). Sin <see cref="PublicUrl"/> https las
    /// invitaciones esperan con un mensaje claro: no se puede armar el enlace de activación.
    /// </summary>
    public interface IPortalSettings
    {
        /// <summary>Dirección pública del portal (https), de donde sale el enlace <c>/activar?token=...</c>.</summary>
        string? PublicUrl { get; }

        bool IsConfigured { get; }
    }
}
