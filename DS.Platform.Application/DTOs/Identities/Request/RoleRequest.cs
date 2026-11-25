using System.ComponentModel.DataAnnotations;

namespace DS.Platform.Application.DTOs.Identities.Request
{
    public class RoleRequest
    {
        public string? Id { get; set; }

        [Required]
        public string? Name { get; set; }
        public string? Description { get; set; }
    }
}