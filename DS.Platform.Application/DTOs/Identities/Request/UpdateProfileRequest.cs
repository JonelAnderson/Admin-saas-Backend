using System.ComponentModel.DataAnnotations;

namespace DS.Platform.Application.DTOs.Identities.Request
{
    /// <summary>Edición de un usuario (JSON). La foto va aparte: POST api/identity/account/profile-picture/{userId}.</summary>
    public class UpdateProfileRequest
    {
        public string ProfileId { get; set; } = string.Empty;

        [Required]
        public string FirstName { get; set; } =string.Empty;

        [Required]
        public string LastName { get; set; } = string.Empty;
        public string? PhoneNumber { get; set; }
        public string? Email { get; set; }
        public string? UserLogin { get; set; }
        public int ChargeId { get; set; }
        public string UserId { get; set; } =string.Empty;
    }
}