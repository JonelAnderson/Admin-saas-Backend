using System.Collections.Generic;

namespace DS.Platform.Application.DTOs.Identities.Response
{
    public class GetAllRolesResponse
    {
        public IEnumerable<RoleResponse>? Roles { get; set; }
    }
}