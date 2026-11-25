using System.ComponentModel.DataAnnotations;

namespace DS.Platform.Application.DTOs.Identities.Request
{
    public class ChangePasswordRequest
    {
        public string ProfileId { get; set; } = string.Empty;
        [Required]
        public string Password { get; set; } = string.Empty;

        [Required]
        public string NewPassword { get; set; } = string.Empty;

        [Required]
        public string ConfirmNewPassword { get; set; } = string.Empty;
    }
}