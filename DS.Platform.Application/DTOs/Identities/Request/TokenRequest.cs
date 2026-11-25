using System.ComponentModel.DataAnnotations;

namespace DS.Platform.Application.DTOs.Identities.Request
{
    public class TokenRequest
    {
        [Required]
        public string UserName { get; set; } = string.Empty;

        [Required]
        public string Password { get; set; } = string.Empty;
        /// <summary>Token del dispositivo recordado: si vale, no se pide el código MFA.</summary>
        public string? DeviceToken { get; set; }
    }
}