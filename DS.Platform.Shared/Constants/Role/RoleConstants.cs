using System;
using System.Linq;

namespace DS.Platform.Shared.Constants.Role
{
    /// <summary>
    /// Nombres de los roles del sistema de la consola (los usan el código, el JWT y el script de menús, así que no se renombran
    /// desde la pantalla). Todos los crea <c>DatabaseSeeder</c> con sus permisos por defecto (<see cref="SystemRoleCatalog"/>).
    /// </summary>
    public static class RoleConstants
    {
        /// <summary>Superadministrador: todas las pantallas y todos los permisos, también usuarios y roles. No se edita.</summary>
        public const string SuperAdminRole = "SuperAdmin";

        /// <summary>Nombre reservado (rol de plataforma del ERP): nadie puede crear un rol con este nombre.</summary>
        public const string PlatformAdminRole = "PlatformAdmin";

        /// <summary>Administrador de la consola: recibe todo al crearse y luego se configura desde Roles.</summary>
        public const string AdministratorRole = "Administrador";

        /// <summary>Clientes, suscripciones, registrar y anular pagos, prórrogas.</summary>
        public const string BillingRole = "Cobranzas";

        /// <summary>Ver clientes, instancias y empresas emisoras; reintentar acciones; no cobra ni suspende.</summary>
        public const string SupportRole = "Soporte";

        /// <summary>Solo consulta: nunca protege una escritura.</summary>
        public const string ReadOnlyRole = "Lectura";

        /// <summary>Rol por defecto de los usuarios nuevos: solo inicio hasta que se le asignen permisos.</summary>
        public const string BasicRole = "Básico";

        public const string DefaultPassword = "123Pa$$word!";

        /// <summary>Roles con acceso total (incluidos otros SuperAdmin).</summary>
        public static readonly string[] PlatformRoles = [SuperAdminRole, PlatformAdminRole];

        /// <summary>
        /// Sin distinguir mayúsculas, como Identity (que busca los roles por nombre normalizado): "superadmin" también es
        /// de plataforma.
        /// </summary>
        public static bool IsPlatformRole(string? roleName)
            => roleName is not null && PlatformRoles.Contains(roleName.Trim(), StringComparer.OrdinalIgnoreCase);
    }
}
