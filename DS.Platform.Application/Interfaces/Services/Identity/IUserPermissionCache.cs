namespace DS.Platform.Application.Interfaces.Services.Identity
{
    /// <summary>
    /// Permisos efectivos de cada usuario en memoria (5 minutos), los que leen la autorización por política y
    /// <c>ICurrentUserService.HasPermission</c>. Se invalida al cambiar el sello de sesión del usuario (roles, contraseña, MFA,
    /// desactivación) y, para todos, al cambiar los permisos de un rol: un permiso retirado deja de valer en el siguiente request.
    /// </summary>
    public interface IUserPermissionCache
    {
        Task<List<string>> GetAsync(string userId, Func<Task<List<string>?>> load);

        void Invalidate(string userId);

        /// <summary>Tras cambiar los permisos de un rol (los roles son compartidos): todos vuelven a leerlos.</summary>
        void InvalidateAll();
    }
}
