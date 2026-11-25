using System.ComponentModel.DataAnnotations;

namespace DS.Platform.Application.DTOs.Identities.Request
{
    public class ForgotPasswordRequest
    {
        [Required]
        [EmailAddress]
        public string? Email { get; set; }
    }
}