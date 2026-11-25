using System.Collections.Generic;

namespace DS.Platform.Application.DTOs.Identities.Request
{
    public class UpdateRoleMenusRequest
    {
        public string RoleId { get; set; } = string.Empty;
        public List<int> MenuIds { get; set; } = new List<int>();
    }
}
