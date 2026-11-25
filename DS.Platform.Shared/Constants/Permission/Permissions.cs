using System.ComponentModel;
using System.Reflection;

namespace DS.Platform.Shared.Constants.Permission
{
    /// <summary>
    /// Catálogo de permisos de la consola de plataforma. Un módulo nuevo agrega aquí su clase (Menu, View, Create, Edit…) y
    /// asigna sus permisos a los roles que correspondan en <c>SystemRoleCatalog</c>.
    /// </summary>
    public static class Permissions
    {
        [DisplayName("Users")]
        [Description("Users Permissions")]
        public static class Users
        {
            public const string Menu = "Permissions.Users.Menu";
            public const string View = "Permissions.Users.View";
            public const string Create = "Permissions.Users.Create";
            public const string Edit = "Permissions.Users.Edit";
            public const string Delete = "Permissions.Users.Delete";
            public const string ChangeState = "Permissions.Users.ChangeState";
            /// <summary>Restablecer el MFA de otro usuario y desbloquear cuentas.</summary>
            public const string ResetMfa = "Permissions.Users.ResetMfa";
        }

        [DisplayName("Roles")]
        [Description("Roles Permissions")]
        public static class Roles
        {
            public const string Menu = "Permissions.Roles.Menu";
            public const string View = "Permissions.Roles.View";
            public const string Create = "Permissions.Roles.Create";
            public const string Edit = "Permissions.Roles.Edit";
            public const string Delete = "Permissions.Roles.Delete";
        }

        [DisplayName("Role Claims")]
        [Description("Role Claims Permissions")]
        public static class RoleClaims
        {
            public const string View = "Permissions.RoleClaims.View";
            public const string Create = "Permissions.RoleClaims.Create";
            public const string Edit = "Permissions.RoleClaims.Edit";
            public const string Delete = "Permissions.RoleClaims.Delete";
        }

        [DisplayName("Preferences")]
        [Description("Preferences Permissions")]
        public static class Preferences
        {
            public const string ChangeLanguage = "Permissions.Preferences.ChangeLanguage";
        }

        [DisplayName("Dashboards")]
        [Description("Dashboards Permissions")]
        public static class Dashboards
        {
            public const string Menu = "Permissions.Dashboards.Menu";
            public const string View = "Permissions.Dashboards.View";
        }

        [DisplayName("Hangfire")]
        [Description("Hangfire Permissions")]
        public static class Hangfire
        {
            public const string Menu = "Permissions.Hangfire.Menu";
            public const string View = "Permissions.Hangfire.View";
        }

        [DisplayName("Audit Trails")]
        [Description("Audit Trails Permissions")]
        public static class AuditTrails
        {
            public const string Menu = "Permissions.AuditTrails.Menu";
            public const string View = "Permissions.AuditTrails.View";
            public const string Print = "Permissions.AuditTrails.Print";
            public const string Download = "Permissions.AuditTrails.Download";
        }

        [DisplayName("Menus")]
        [Description("Menus Permissions")]
        public static class Menus
        {
            public const string Menu = "Permissions.Menus.Menu";
            public const string View = "Permissions.Menus.View";
            public const string Create = "Permissions.Menus.Create";
            public const string Edit = "Permissions.Menus.Edit";
            public const string ChangeState = "Permissions.Menus.ChangeState";
            public const string Delete = "Permissions.Menus.Delete";
        }

        /// <summary>Planes comerciales del ERP y de la API SUNAT (módulo de referencia de la consola).</summary>
        [DisplayName("Plans")]
        [Description("Plans Permissions")]
        public static class Plans
        {
            public const string Menu = "Permissions.Plans.Menu";
            public const string View = "Permissions.Plans.View";
            public const string Create = "Permissions.Plans.Create";
            public const string Edit = "Permissions.Plans.Edit";
            public const string ChangeState = "Permissions.Plans.ChangeState";
            public const string Delete = "Permissions.Plans.Delete";
        }

        /// <summary>Clientes de la plataforma (quien contrata el ERP y / o la API SUNAT).</summary>
        [DisplayName("Customers")]
        [Description("Customers Permissions")]
        public static class Customers
        {
            public const string Menu = "Permissions.Customers.Menu";
            public const string View = "Permissions.Customers.View";
            public const string Create = "Permissions.Customers.Create";
            public const string Edit = "Permissions.Customers.Edit";
            public const string ChangeState = "Permissions.Customers.ChangeState";
            public const string Delete = "Permissions.Customers.Delete";
        }

        /// <summary>Suscripciones: qué cliente tiene qué plan, prórrogas, suspensión y cancelación.</summary>
        [DisplayName("Subscriptions")]
        [Description("Subscriptions Permissions")]
        public static class Subscriptions
        {
            public const string Menu = "Permissions.Subscriptions.Menu";
            public const string View = "Permissions.Subscriptions.View";
            public const string Create = "Permissions.Subscriptions.Create";
            public const string Edit = "Permissions.Subscriptions.Edit";
            /// <summary>Prórroga manual de "pagado hasta" (con motivo).</summary>
            public const string Extend = "Permissions.Subscriptions.Extend";
            /// <summary>Suspender y reactivar a mano (con motivo).</summary>
            public const string Suspend = "Permissions.Subscriptions.Suspend";
            public const string Cancel = "Permissions.Subscriptions.Cancel";
        }

        /// <summary>Pagos de los clientes: registrar (extiende la suscripción) y anular con motivo. Nunca se editan.</summary>
        [DisplayName("Payments")]
        [Description("Payments Permissions")]
        public static class Payments
        {
            public const string Menu = "Permissions.Payments.Menu";
            public const string View = "Permissions.Payments.View";
            public const string Create = "Permissions.Payments.Create";
            public const string Void = "Permissions.Payments.Void";
        }

        /// <summary>Empresas emisoras de la API SUNAT vinculadas a clientes: vincular, sincronizar y desvincular.</summary>
        [DisplayName("ApiSunatIssuers")]
        [Description("API SUNAT issuers Permissions")]
        public static class ApiSunatIssuers
        {
            public const string Menu = "Permissions.ApiSunatIssuers.Menu";
            public const string View = "Permissions.ApiSunatIssuers.View";
            public const string Link = "Permissions.ApiSunatIssuers.Link";
            public const string Sync = "Permissions.ApiSunatIssuers.Sync";
            public const string Unlink = "Permissions.ApiSunatIssuers.Unlink";
        }

        /// <summary>Instancias del ERP de los clientes: su clave de licencia, estado y último contacto.</summary>
        [DisplayName("Instances")]
        [Description("ERP instances Permissions")]
        public static class Instances
        {
            public const string Menu = "Permissions.Instances.Menu";
            public const string View = "Permissions.Instances.View";
            public const string Create = "Permissions.Instances.Create";
            public const string Edit = "Permissions.Instances.Edit";
            /// <summary>Generar una clave de licencia nueva (la anterior deja de valer).</summary>
            public const string RotateKey = "Permissions.Instances.RotateKey";
            public const string Delete = "Permissions.Instances.Delete";
        }

        /// <summary>Bandeja de acciones remotas (suspender / reactivar emisores en la API SUNAT): consultar y reintentar.</summary>
        [DisplayName("RemoteActions")]
        [Description("Remote actions Permissions")]
        public static class RemoteActions
        {
            public const string Menu = "Permissions.RemoteActions.Menu";
            public const string View = "Permissions.RemoteActions.View";
            public const string Retry = "Permissions.RemoteActions.Retry";
        }

        /// <summary>Despliegues de instancias del ERP en Coolify (fase C5): montar un stack, actualizarlo de versión, reintentar y cancelar.</summary>
        [DisplayName("Deployments")]
        [Description("Deployments Permissions")]
        public static class Deployments
        {
            public const string Menu = "Permissions.Deployments.Menu";
            public const string View = "Permissions.Deployments.View";
            public const string Provision = "Permissions.Deployments.Provision";
            public const string Upgrade = "Permissions.Deployments.Upgrade";
            public const string Retry = "Permissions.Deployments.Retry";
        }

        /// <summary>Acceso de personas de los clientes al portal de la API SUNAT (fase C6): invitar / reinvitar y habilitar / deshabilitar.</summary>
        [DisplayName("PortalAccess")]
        [Description("Portal access Permissions")]
        public static class PortalAccess
        {
            public const string Menu = "Permissions.PortalAccess.Menu";
            public const string View = "Permissions.PortalAccess.View";
            public const string Invite = "Permissions.PortalAccess.Invite";
            public const string Manage = "Permissions.PortalAccess.Manage";
        }

        /// <summary>Entrega de las credenciales de la API SUNAT de un emisor a la instancia del ERP de su cliente (fase C6b): pedir, reintentar y cancelar.</summary>
        [DisplayName("ApiCredentialDeliveries")]
        [Description("API credential deliveries Permissions")]
        public static class ApiCredentialDeliveries
        {
            public const string Menu = "Permissions.ApiCredentialDeliveries.Menu";
            public const string View = "Permissions.ApiCredentialDeliveries.View";
            public const string Create = "Permissions.ApiCredentialDeliveries.Create";
            public const string Retry = "Permissions.ApiCredentialDeliveries.Retry";
            public const string Cancel = "Permissions.ApiCredentialDeliveries.Cancel";
        }

        /// <summary>Bandeja de avisos por correo a los clientes (vencimientos, suspensión, constancias, certificados): consultar y reintentar.</summary>
        [DisplayName("Notifications")]
        [Description("Notifications Permissions")]
        public static class Notifications
        {
            public const string Menu = "Permissions.Notifications.Menu";
            public const string View = "Permissions.Notifications.View";
            public const string Retry = "Permissions.Notifications.Retry";
        }

        /// <summary>
        /// Returns a list of Permissions.
        /// </summary>
        public static List<string> GetRegisteredPermissions()
        {
            var permissions = new List<string>();
            foreach (var prop in typeof(Permissions).GetNestedTypes().SelectMany(c => c.GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)))
            {
                var propertyValue = prop.GetValue(null);
                if (propertyValue is not null)
                    permissions.Add(propertyValue.ToString()!);
            }
            return permissions;
        }
    }
}
