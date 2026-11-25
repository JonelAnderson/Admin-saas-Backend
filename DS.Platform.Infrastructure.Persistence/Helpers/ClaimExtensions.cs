using DS.Platform.Application.DTOs.Identities.Response;
using DS.Platform.Domain.Entities.Identity;
using DS.Platform.Shared.Constants.Permission;
using Microsoft.AspNetCore.Identity;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Security.Claims;
using System.Threading.Tasks;

namespace DS.Platform.Infrastructure.Helpers
{
    public static class ClaimsHelper
    {
        public static void GetAllPermissions(this List<RoleClaimResponse> allPermissions)
        {
            foreach (var module in typeof(Permissions).GetNestedTypes())
            {
                CollectPermissionFields(module, module, allPermissions);
            }
        }

        private static void CollectPermissionFields(Type type, Type rootModule, List<RoleClaimResponse> allPermissions)
        {
            var moduleName = string.Empty;
            var moduleDescription = string.Empty;

            if (rootModule.GetCustomAttributes(typeof(DisplayNameAttribute), true)
                .FirstOrDefault() is DisplayNameAttribute displayNameAttribute)
                moduleName = displayNameAttribute.DisplayName;

            if (rootModule.GetCustomAttributes(typeof(DescriptionAttribute), true)
                .FirstOrDefault() is DescriptionAttribute descriptionAttribute)
                moduleDescription = descriptionAttribute.Description;

            if (type != rootModule && type.GetCustomAttributes(typeof(DisplayNameAttribute), true)
                .FirstOrDefault() is DisplayNameAttribute nestedDisplay)
            {
                moduleName = string.IsNullOrEmpty(moduleName)
                    ? nestedDisplay.DisplayName
                    : $"{moduleName} - {nestedDisplay.DisplayName}";
            }

            foreach (var fi in type.GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly))
            {
                if (fi.FieldType != typeof(string))
                    continue;
                var propertyValue = fi.GetValue(null);
                if (propertyValue is not null)
                {
                    allPermissions.Add(new RoleClaimResponse
                    {
                        Value = propertyValue.ToString(),
                        Type = ApplicationClaimTypes.Permission,
                        Group = moduleName,
                        Description = moduleDescription
                    });
                }
            }

            foreach (var nested in type.GetNestedTypes(BindingFlags.Public))
            {
                CollectPermissionFields(nested, rootModule, allPermissions);
            }
        }

        public static async Task<IdentityResult> AddPermissionClaim(this RoleManager<AdmRole> roleManager, AdmRole role, string permission)
        {
            var allClaims = await roleManager.GetClaimsAsync(role);
            if (!allClaims.Any(a => a.Type == ApplicationClaimTypes.Permission && a.Value == permission))
            {
                return await roleManager.AddClaimAsync(role, new Claim(ApplicationClaimTypes.Permission, permission));
            }

            return IdentityResult.Failed();
        }
    }
}