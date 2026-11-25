using DS.Platform.Application.Interfaces.Services.Identity;
using Microsoft.Extensions.Caching.Memory;

namespace DS.Platform.Infrastructure.Services.Identity
{
    /// <summary>
    /// Implementación en memoria de <see cref="IUserPermissionCache"/> (singleton). <see cref="InvalidateAll"/> cambia la
    /// generación que forma parte de la clave, así las entradas anteriores dejan de leerse (y vencen solas).
    /// </summary>
    public sealed class UserPermissionCache(IMemoryCache cache) : IUserPermissionCache
    {
        internal static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(5);

        private long _generation;

        public async Task<List<string>> GetAsync(string userId, Func<Task<List<string>?>> load)
        {
            var key = Key(userId);
            if (cache.TryGetValue(key, out List<string>? permissions) && permissions is not null)
                return permissions;

            var loaded = await load();
            if (loaded is null)
                return [];
            cache.Set(key, loaded, CacheTtl);
            return loaded;
        }

        public void Invalidate(string userId)
        {
            if (!string.IsNullOrEmpty(userId))
                cache.Remove(Key(userId));
        }

        public void InvalidateAll() => Interlocked.Increment(ref _generation);

        private string Key(string userId) => $"User_Permissions_{Interlocked.Read(ref _generation)}_{userId}";
    }
}
