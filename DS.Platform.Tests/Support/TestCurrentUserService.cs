using DS.Platform.Application.Interfaces.Services;

namespace DS.Platform.Tests.Support
{
    /// <summary>Usuario de la petición en las pruebas (la consola no es multiempresa: no hay empresa ni sucursal).</summary>
    public sealed class TestCurrentUserService : ICurrentUserService
    {
        public string? UserId { get; init; } = "test-user";
        public bool IsAuthenticated { get; init; } = true;
        public bool IsPlatformAdmin { get; init; }
        public HashSet<string> Permissions { get; init; } = [];

        public bool HasPermission(string permission) => Permissions.Contains(permission);

        /// <summary>Usuario autenticado común (sin permisos especiales).</summary>
        public static TestCurrentUserService User(string userId = "test-user") => new() { UserId = userId };

        public static TestCurrentUserService NoContext() => new() { UserId = null, IsAuthenticated = false };
    }

    public sealed class FixedDateTimeService : IDateTimeService
    {
        public DateTime NowUtc { get; } = new(2026, 10, 25, 12, 0, 0, DateTimeKind.Utc);
    }
}
