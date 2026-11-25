using DS.Platform.Application.Interfaces.Services;
using DS.Platform.Shared.Constants.Permission;
using DS.Platform.Shared.Constants.Role;
using Microsoft.AspNetCore.Http;
using System.Linq;
using System.Security.Claims;

namespace DS.Platform.WebApi.Services
{
    public class CurrentUserService : ICurrentUserService
    {
        private readonly IHttpContextAccessor _httpContextAccessor;

        public CurrentUserService(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        public string? UserId =>
            _httpContextAccessor.HttpContext?.User?.FindFirstValue(ClaimTypes.NameIdentifier);

        public bool IsAuthenticated =>
            _httpContextAccessor.HttpContext?.User?.Identity?.IsAuthenticated == true;

        public bool IsPlatformAdmin =>
            _httpContextAccessor.HttpContext?.User is { } user
            && RoleConstants.PlatformRoles.Any(user.IsInRole);

        public bool HasPermission(string permission) =>
            _httpContextAccessor.HttpContext?.User?.HasClaim(ApplicationClaimTypes.Permission, permission) == true;
    }
}
