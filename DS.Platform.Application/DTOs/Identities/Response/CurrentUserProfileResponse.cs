namespace DS.Platform.Application.DTOs.Identities.Response
{
    /// <summary>
    /// Datos del usuario conectado para la pantalla (barra superior, Seguridad). Salen de GET api/identity/account/me y no
    /// del JWT: el token ya no lleva datos personales (correo, celular, usuario, foto).
    /// </summary>
    public class CurrentUserProfileResponse
    {
        public string Id { get; set; } = string.Empty;
        public string? UserName { get; set; }
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        /// <summary>Nombres y apellidos, o el usuario si no tiene nombres.</summary>
        public string FullName { get; set; } = string.Empty;
        public string? Email { get; set; }
        public string? ProfilePictureUrl { get; set; }
    }
}
