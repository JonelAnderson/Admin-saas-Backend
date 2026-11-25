using System.Collections.Generic;

namespace DS.Platform.Application.DTOs.Identities.Response
{
    public class UserWithSubordinatesAndProspectsResponseDto
    {
        public string Id { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string UserId { get; set; } = string.Empty;

        public IEnumerable<UserWithSubordinatesAndProspectsResponseDto>? Subordinates { get; set; }
    }
}
