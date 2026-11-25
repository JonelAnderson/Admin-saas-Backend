using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DS.Platform.Application.DTOs.Identities.Response
{
    public class UserByIdResponse
    {
        public string? Id { get; set; }
        public string? UserName { get; set; }
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public string? Email { get; set; }
        public int? ChargeId { get; set; }
        public string? UserId { get; set; }
        public bool IsActive { get; set; }
        public bool EmailConfirmed { get; set; }
        public string? PhoneNumber { get; set; }
        public string? ProfilePictureDataUrl { get; set; }
    }
}
