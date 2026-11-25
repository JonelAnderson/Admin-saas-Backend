using System.Collections.Generic;

namespace DS.Platform.Application.DTOs.Identities.Response
{
    public class UserRolesResponse
    {
        public List<UserRoleModel> UserRoles { get; set; } = new();
    }

    public class UserRoleModel
    {
        public string? RoleName { get; set; }
        public string? RoleDescription { get; set; }
        public bool Selected { get; set; }
    }
}