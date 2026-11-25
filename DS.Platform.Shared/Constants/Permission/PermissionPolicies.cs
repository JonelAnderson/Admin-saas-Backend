namespace DS.Platform.Shared.Constants.Permission
{
    /// <summary>
    /// Políticas de "cualquiera de estos permisos" (separados por <see cref="Separator"/>), para lecturas que usan varias
    /// pantallas. Van fuera de <see cref="Permissions"/> para que no entren al catálogo de permisos. La consola aún no tiene
    /// ninguna: se agregan cuando una lectura la comparten varias pantallas (patrón del ERP).
    /// </summary>
    public static class PermissionPolicies
    {
        public const char Separator = '|';
    }
}
