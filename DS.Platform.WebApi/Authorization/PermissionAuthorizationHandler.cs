using DS.Platform.Application.Interfaces.Services.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Caching.Memory;
using DS.Platform.Shared.Constants.Role;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace DS.Platform.WebApi.Authorization
{
    public class PermissionAuthorizationHandler : AuthorizationHandler<PermissionRequirement>
    {
        private readonly ITokenService _tokenService;
        private readonly IUserPermissionCache _cache;

        public PermissionAuthorizationHandler(ITokenService tokenService, IUserPermissionCache cache)
        {
            _tokenService = tokenService;
            _cache = cache;
        }

        protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, PermissionRequirement requirement)
        {
            if (context.User == null)
            {
                return;
            }

            // SuperAdmin (o el antiguo PlatformAdmin) tiene todos los permisos, también los que se agreguen después. El rol viaja
            // en el JWT; si se lo quitan, el nuevo security stamp rechaza ese token.
            if (RoleConstants.PlatformRoles.Any(context.User.IsInRole))
            {
                context.Succeed(requirement);
                return;
            }

            // Get User ID from Claims
            var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
            {
                return;
            }

            // Caché compartida con OnTokenValidated; se invalida al cambiar roles o permisos (IUserPermissionCache).
            var userPermissions = await _cache.GetAsync(userId, async () =>
            {
                var result = await _tokenService.GetPermissionsAsync(userId);
                return result.Succeeded ? result.Data : null;
            });

            // Check if user has the required permission
            if (userPermissions != null && requirement.AnyOf.Any(userPermissions.Contains))
            {
                context.Succeed(requirement);
            }
        }
    }
}
