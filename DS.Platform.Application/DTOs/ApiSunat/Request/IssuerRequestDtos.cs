namespace DS.Platform.Application.DTOs.ApiSunat.Request
{
    /// <summary>Vincula una empresa de la API SUNAT a un cliente: sus datos se leen de la API, no del formulario.</summary>
    public class LinkIssuerRequestDto
    {
        public int CustomerId { get; set; }
        /// <summary>Id de la empresa en la API SUNAT.</summary>
        public long TenantId { get; set; }
        public string? Notes { get; set; }
    }
}
