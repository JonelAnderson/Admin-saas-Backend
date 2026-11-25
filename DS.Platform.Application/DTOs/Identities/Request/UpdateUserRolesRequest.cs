using DS.Platform.Application.DTOs.Identities.Response;
using System.Collections.Generic;

namespace DS.Platform.Application.DTOs.Identities.Request
{
    public class UpdateUserRolesRequest
    {
        public string? UserId { get; set; }
        public IList<UserRoleModel>? UserRoles { get; set; }
    }
}