using DS.Platform.Application.Interfaces.Services;
using DS.Platform.Domain.Entities.Identity;
using DS.Platform.Shared.Constants.Role;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace DS.Platform.Infrastructure.Services.Identity
{
    /// <summary>
    /// A qué usuarios llega quien administra usuarios en la consola (no es multiempresa). SuperAdmin: a todos. Los demás
    /// (con el permiso de usuarios): a todos menos a los SuperAdmin, que solo administra otro SuperAdmin.
    /// </summary>
    public static class UserAdminScope
    {
        public const string DeniedMessage = "No tiene acceso a este usuario: solo un SuperAdmin administra a otro SuperAdmin.";
        public const string ReportsToDeniedMessage = "El usuario al que reporta no existe o no tiene acceso a él.";

        /// <summary>Usuarios visibles para el actual (consulta sin ejecutar).</summary>
        public static async Task<IQueryable<AdmUser>> VisibleAsync(UserManager<AdmUser> users, ICurrentUserService current)
        {
            if (current.IsPlatformAdmin)
            {
                return users.Users;
            }

            var platformIds = await PlatformUserIdsAsync(users);
            return users.Users.Where(u => !platformIds.Contains(u.Id));
        }

        public static async Task<bool> IsVisibleAsync(UserManager<AdmUser> users, ICurrentUserService current, string? userId)
            => !string.IsNullOrEmpty(userId) && await (await VisibleAsync(users, current)).AnyAsync(u => u.Id == userId);

        /// <summary>Puede editar, desactivar, cambiar roles o restablecer el MFA de <paramref name="target"/>.</summary>
        public static Task<bool> CanManageAsync(UserManager<AdmUser> users, ICurrentUserService current, AdmUser target)
            => current.IsPlatformAdmin ? Task.FromResult(true) : IsVisibleAsync(users, current, target.Id);

        private static async Task<List<string>> PlatformUserIdsAsync(UserManager<AdmUser> users)
        {
            var ids = new List<string>();
            foreach (var role in RoleConstants.PlatformRoles)
            {
                ids.AddRange((await users.GetUsersInRoleAsync(role)).Select(u => u.Id));
            }
            return ids;
        }
    }
}
