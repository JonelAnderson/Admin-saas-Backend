using System.ComponentModel.DataAnnotations;

namespace DS.Platform.Application.DTOs.Identities.Response
{
    public class RoleResponse
    {
        public string? Id { get; set; }

        [Required]
        public string? Name { get; set; }
        public string? Description { get; set; }
    }
}