namespace DS.Platform.Application.Configurations
{
    public class AppConfiguration
    {
        public string? Secret { get; set; }
        public string? Issuer { get; set; }
        /// <summary>Respaldo antiguo (horas). Lo que manda es <see cref="AccessTokenMinutes"/>.</summary>
        public string? Expires { get; set; }

        /// <summary>Minutos de vida del token de acceso (JWT). El frontend lo renueva antes de que venza con el refresh token (7 días).</summary>
        public int AccessTokenMinutes { get; set; } = 30;

        public bool BehindSSLProxy { get; set; }

        public string? ProxyIP { get; set; }

        public string? ApplicationUrl { get; set; }
    }
}