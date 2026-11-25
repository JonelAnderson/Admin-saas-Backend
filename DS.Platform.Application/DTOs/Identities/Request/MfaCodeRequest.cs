namespace DS.Platform.Application.DTOs.Identities.Request
{
    /// <summary>Código actual de la app autenticadora (para regenerar los códigos de recuperación).</summary>
    public class MfaCodeRequest
    {
        public string? Code { get; set; }
    }
}
