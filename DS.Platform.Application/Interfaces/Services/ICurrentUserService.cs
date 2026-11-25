using DS.Platform.Application.Interfaces.Common;

namespace DS.Platform.Application.Interfaces.Services
{
    /// <summary>Usuario de la petición. La consola no es multiempresa: no hay empresa, sucursal ni almacén de contexto.</summary>
    public interface ICurrentUserService : IService
    {
        string? UserId { get; }
        bool IsAuthenticated { get; }

        /// <summary>SuperAdmin (o el antiguo PlatformAdmin): administra todo, incluidos otros SuperAdmin.</summary>
        bool IsPlatformAdmin { get; }

        /// <summary>True when the user's token carries this permission claim (Permissions.*).</summary>
        bool HasPermission(string permission);
    }
}
