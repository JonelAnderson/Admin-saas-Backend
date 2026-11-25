namespace DS.Platform.Domain.Enums
{
    /// <summary>Qué le falta cumplir en la API SUNAT a un acceso al portal (fase C6).</summary>
    public enum PortalWorkType
    {
        /// <summary>Crear (o actualizar) el usuario del portal y mandarle el enlace para fijar su contraseña.</summary>
        Invite = 1,

        /// <summary>Habilitar o deshabilitar al usuario ya creado (según <c>IsEnabled</c> del acceso).</summary>
        SetState = 2
    }
}
