using System.Collections.Generic;

namespace DS.Platform.Application.DTOs.Identities.Response
{
    public class GetAllUsersResponse
    {
        public IEnumerable<UserResponse>? Users { get; set; }
    }
}