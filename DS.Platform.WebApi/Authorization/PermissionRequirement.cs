using DS.Platform.Shared.Constants.Permission;
using Microsoft.AspNetCore.Authorization;
using System;
using System.Collections.Generic;

namespace DS.Platform.WebApi.Authorization
{
    public class PermissionRequirement : IAuthorizationRequirement
    {
        /// <summary>Nombre de la política: un permiso, o varios separados por <see cref="PermissionPolicies.Separator"/>.</summary>
        public string Permission { get; }

        /// <summary>Basta con tener uno (una política simple tiene uno solo).</summary>
        public IReadOnlyList<string> AnyOf { get; }

        public PermissionRequirement(string permission)
        {
            Permission = permission;
            AnyOf = permission.Split(PermissionPolicies.Separator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        }
    }
}
