using DS.Platform.Application.Interfaces.Services.Identity;
using DS.Platform.Infrastructure.Persistence.Contexts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using System.Security.Cryptography;
using System.Text;

namespace DS.Platform.Infrastructure.Services.Identity
{
    /// <summary>
    /// Sello de sesión del JWT (claim <see cref="ClaimType"/>): los primeros 16 bytes del SHA-256 del security stamp del usuario
    /// (nunca el stamp en claro). Se valida en cada request contra el stamp vigente, con caché de <see cref="CacheTtl"/> por
    /// usuario: en esta instancia la revocación es inmediata (<see cref="Invalidate"/>), en otras instancias tarda como mucho ese
    /// tiempo. Infraestructura de seguridad (Identity), como <c>IdentityService</c>.
    /// </summary>
    public sealed class SessionStampService(IServiceScopeFactory scopes, IMemoryCache cache, IUserPermissionCache permissions) : ISessionStampService
    {
        public const string ClaimType = "sst";

        internal static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(30);

        private sealed record Entry(string? StampHash, bool Active);

        /// <summary>Huella del security stamp que va en el JWT.</summary>
        public static string Hash(string securityStamp)
            => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(securityStamp)), 0, 16);

        public async Task<bool> IsCurrentAsync(string userId, string? tokenStamp, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(tokenStamp))
                return false;

            var key = CacheKey(userId);
            if (!cache.TryGetValue(key, out Entry? entry) || entry is null)
            {
                using var scope = scopes.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var user = await db.Users.AsNoTracking()
                    .Where(u => u.Id == userId)
                    .Select(u => new { u.SecurityStamp, u.IsActive, u.IsDeleted })
                    .FirstOrDefaultAsync(cancellationToken);
                entry = user is null
                    ? new Entry(null, false)
                    : new Entry(string.IsNullOrEmpty(user.SecurityStamp) ? null : Hash(user.SecurityStamp), user.IsActive && !user.IsDeleted);
                cache.Set(key, entry, CacheTtl);
            }

            return entry.Active && entry.StampHash is not null
                   && CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(entry.StampHash), Encoding.ASCII.GetBytes(tokenStamp));
        }

        public void Invalidate(string userId)
        {
            if (!string.IsNullOrEmpty(userId))
            {
                cache.Remove(CacheKey(userId));
                // Quien cambia el sello (roles, contraseña, MFA, desactivar) también cambia lo que el usuario puede hacer.
                permissions.Invalidate(userId);
            }
        }

        private static string CacheKey(string userId) => $"User_SessionStamp_{userId}";
    }
}
