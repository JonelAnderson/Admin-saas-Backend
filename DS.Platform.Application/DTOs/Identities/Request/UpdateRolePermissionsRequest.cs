using System.Collections.Generic;

namespace DS.Platform.Application.DTOs.Identities.Request
{
    public class UpdateRolePermissionsRequest
    {
        public string RoleId { get; set; } = string.Empty;
        public List<int> PermissionIds { get; set; } = new List<int>();
    }
}
