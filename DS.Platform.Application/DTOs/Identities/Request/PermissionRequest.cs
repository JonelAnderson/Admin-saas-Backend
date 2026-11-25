using System.Collections.Generic;

namespace DS.Platform.Application.DTOs.Identities.Request
{
    public class PermissionRequest
    {
        public string? RoleId { get; set; }
        public IList<RoleClaimRequest>? RoleClaims { get; set; }
    }
}