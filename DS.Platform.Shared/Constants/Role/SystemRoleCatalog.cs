using DS.Platform.Shared.Constants.Permission;
using System;
using System.Collections.Generic;
using System.Linq;
using P = DS.Platform.Shared.Constants.Permission.Permissions;

namespace DS.Platform.Shared.Constants.Role
{
    /// <summary>Cómo recibe permisos un rol del sistema al crearse.</summary>
    public enum SystemRoleGrant
    {
        /// <summary>Solo los de <see cref="SystemRoleDefinition.Permissions"/>.</summary>
        Listed,

        /// <summary>Todos los del catálogo, una sola vez al crear el rol (luego se configura desde Roles).</summary>
        AllOnCreate,

        /// <summary>Todos, sincronizados en cada arranque (SuperAdmin: no se edita).</summary>
        AllAlways,

        /// <summary>Solo lectura: todo permiso de consulta del catálogo (<see cref="SystemRoleCatalog.IsReadOnly"/>).</summary>
        ReadOnly
    }

    /// <param name="Name">Nombre del rol (identificador).</param>
    /// <param name="Description">Para qué sirve.</param>
    /// <param name="LegacyName">Nombre anterior: si el rol existe con ese nombre, el seeder lo renombra (conserva Id, permisos,
    /// usuarios y menús). La consola nace sin roles antiguos, así que hoy ninguno lo usa.</param>
    public sealed record SystemRoleDefinition(
        string Name, string Description, SystemRoleGrant Grant, IReadOnlyList<string> Permissions, string? LegacyName = null);

    /// <summary>
    /// Roles de la consola que crea el seeder con sus permisos por defecto. Los permisos por defecto se ponen <b>solo al crear
    /// el rol</b>: después mandan los cambios hechos en Roles → Permisos, salvo SuperAdmin, que siempre tiene todos. Cada módulo
    /// nuevo agrega sus permisos a Cobranzas / Soporte según corresponda (Lectura y Administrador los toman solos).
    /// </summary>
    public static class SystemRoleCatalog
    {
        /// <summary>Permisos que todo usuario interno necesita (tablero de inicio e idioma).</summary>
        private static readonly string[] Staff = [P.Dashboards.Menu, P.Dashboards.View, P.Preferences.ChangeLanguage];

        public static readonly IReadOnlyList<SystemRoleDefinition> Roles =
        [
            new(RoleConstants.SuperAdminRole,
                "Todas las pantallas y funciones, también usuarios y roles. No se puede editar ni eliminar.",
                SystemRoleGrant.AllAlways, []),

            new(RoleConstants.AdministratorRole,
                "Administra la consola: permisos y menús configurables desde Roles.",
                SystemRoleGrant.AllOnCreate, []),

            new(RoleConstants.BillingRole,
                "Clientes, planes, suscripciones y pagos: registrar y anular pagos, prórrogas y suspensiones.",
                SystemRoleGrant.Listed,
                [.. Staff, P.Plans.Menu, P.Plans.View,
                 P.Customers.Menu, P.Customers.View, P.Customers.Create, P.Customers.Edit, P.Customers.ChangeState,
                 P.Subscriptions.Menu, P.Subscriptions.View, P.Subscriptions.Create, P.Subscriptions.Edit,
                 P.Subscriptions.Extend, P.Subscriptions.Suspend, P.Subscriptions.Cancel,
                 P.Payments.Menu, P.Payments.View, P.Payments.Create, P.Payments.Void,
                 P.ApiSunatIssuers.Menu, P.ApiSunatIssuers.View, P.ApiSunatIssuers.Link, P.ApiSunatIssuers.Sync, P.ApiSunatIssuers.Unlink,
                 P.RemoteActions.Menu, P.RemoteActions.View, P.RemoteActions.Retry,
                 P.Notifications.Menu, P.Notifications.View, P.Notifications.Retry,
                 P.Instances.Menu, P.Instances.View, P.Instances.Create, P.Instances.Edit, P.Instances.RotateKey,
                 P.Deployments.Menu, P.Deployments.View,
                 P.PortalAccess.Menu, P.PortalAccess.View, P.PortalAccess.Invite, P.PortalAccess.Manage,
                 P.ApiCredentialDeliveries.Menu, P.ApiCredentialDeliveries.View]),

            new(RoleConstants.SupportRole,
                "Consulta de clientes, instancias del ERP y empresas emisoras de la API SUNAT; reintenta acciones. No cobra ni suspende.",
                SystemRoleGrant.Listed,
                [.. Staff, P.Plans.Menu, P.Plans.View, P.Customers.Menu, P.Customers.View,
                 P.Subscriptions.Menu, P.Subscriptions.View, P.Payments.Menu, P.Payments.View,
                 P.ApiSunatIssuers.Menu, P.ApiSunatIssuers.View, P.ApiSunatIssuers.Sync,
                 P.RemoteActions.Menu, P.RemoteActions.View, P.RemoteActions.Retry,
                 P.Notifications.Menu, P.Notifications.View,
                 P.Instances.Menu, P.Instances.View,
                 P.Deployments.Menu, P.Deployments.View, P.Deployments.Provision, P.Deployments.Upgrade, P.Deployments.Retry,
                 P.PortalAccess.Menu, P.PortalAccess.View, P.PortalAccess.Invite, P.PortalAccess.Manage,
                 P.ApiCredentialDeliveries.Menu, P.ApiCredentialDeliveries.View, P.ApiCredentialDeliveries.Create,
                 P.ApiCredentialDeliveries.Retry, P.ApiCredentialDeliveries.Cancel]),

            new(RoleConstants.ReadOnlyRole,
                "Consulta de toda la consola, sin crear, editar ni anular.",
                SystemRoleGrant.ReadOnly, []),

            new(RoleConstants.BasicRole,
                "Rol por defecto de los usuarios nuevos: solo inicio hasta que se le asignen permisos.",
                SystemRoleGrant.Listed, [.. Staff]),
        ];

        /// <summary>Roles del sistema: no se renombran ni se eliminan (el seeder los volvería a crear).</summary>
        public static bool IsSystemRole(string? roleName)
            => roleName is not null && Roles.Any(r => string.Equals(r.Name, roleName, StringComparison.Ordinal));

        /// <summary>
        /// Nombres que un rol propio no puede tomar al crearse o renombrarse, sin distinguir mayúsculas: los de plataforma
        /// (<see cref="RoleConstants.PlatformRoles"/>), los del sistema y sus nombres antiguos. Identity compara roles por nombre
        /// normalizado, así que "superadmin" es SuperAdmin.
        /// </summary>
        public static bool IsReservedName(string? roleName)
        {
            if (string.IsNullOrWhiteSpace(roleName))
                return true;
            var name = roleName.Trim();
            return RoleConstants.PlatformRoles.Contains(name, StringComparer.OrdinalIgnoreCase)
                || Roles.Any(r => string.Equals(r.Name, name, StringComparison.OrdinalIgnoreCase)
                                  || string.Equals(r.LegacyName, name, StringComparison.OrdinalIgnoreCase));
        }

        public static SystemRoleDefinition? Find(string roleName)
            => Roles.FirstOrDefault(r => string.Equals(r.Name, roleName, StringComparison.Ordinal));

        /// <summary>Acciones que solo consultan (rol Lectura).</summary>
        private static readonly string[] ReadOnlyActions = ["Menu", "View", "Search", "Print", "Download", "Export", "Reports"];

        /// <summary>true si el permiso solo consulta (su última parte es una acción de lectura).</summary>
        public static bool IsReadOnly(string permission)
        {
            var parts = permission.Split('.');
            if (parts.Length < 3)
            {
                return false;
            }
            return ReadOnlyActions.Contains(parts[^1], StringComparer.Ordinal);
        }
    }
}
