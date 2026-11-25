using DS.Platform.Application.Interfaces.Services.Identity;
using DS.Platform.Domain.Entities.Identity;
using DS.Platform.Infrastructure.Persistence.Contexts;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;

namespace DS.Platform.Infrastructure.Services.Identity
{
    /// <summary>Infraestructura de Identity (como <c>MfaService</c>): dispositivos recordados del login.</summary>
    public sealed class TrustedDeviceService(ApplicationDbContext db) : ITrustedDeviceService
    {
        /// <summary>Días sin entrar desde el dispositivo tras los que se vuelve a pedir el código.</summary>
        public const int InactivityDays = 7;
        /// <summary>Dispositivos recordados por usuario (el más antiguo sale primero).</summary>
        public const int MaxPerUser = 10;

        public static string Hash(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

        public async Task<bool> IsTrustedAsync(string userId, string? deviceToken, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(userId) || string.IsNullOrWhiteSpace(deviceToken) || deviceToken.Length > 200)
                return false;

            var now = DateTime.UtcNow;
            var since = now.AddDays(-InactivityDays);
            // Los vencidos del usuario se borran: ya no sirven.
            var stale = await db.TrustedDevices.Where(d => d.UserId == userId && d.LastUsedOnUtc < since).ToListAsync(cancellationToken);
            db.TrustedDevices.RemoveRange(stale);

            var hash = Hash(deviceToken);
            var device = await db.TrustedDevices.FirstOrDefaultAsync(d => d.UserId == userId && d.TokenHash == hash && d.LastUsedOnUtc >= since, cancellationToken);
            if (device is not null)
                device.LastUsedOnUtc = now;
            if (device is not null || stale.Count > 0)
                await db.SaveChangesAsync(cancellationToken);
            return device is not null;
        }

        public async Task<string> TrustAsync(string userId, string? label, CancellationToken cancellationToken = default)
        {
            var token = WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
            var now = DateTime.UtcNow;
            var existing = await db.TrustedDevices.Where(d => d.UserId == userId).OrderByDescending(d => d.LastUsedOnUtc).ToListAsync(cancellationToken);
            db.TrustedDevices.RemoveRange(existing.Skip(MaxPerUser - 1));
            db.TrustedDevices.Add(new TrustedDevice
            {
                UserId = userId,
                TokenHash = Hash(token),
                Label = string.IsNullOrWhiteSpace(label) ? null : label.Trim()[..Math.Min(label.Trim().Length, 200)],
                CreatedOnUtc = now,
                LastUsedOnUtc = now
            });
            await db.SaveChangesAsync(cancellationToken);
            return token;
        }

        public async Task RevokeAllAsync(string userId, CancellationToken cancellationToken = default)
        {
            var devices = await db.TrustedDevices.Where(d => d.UserId == userId).ToListAsync(cancellationToken);
            if (devices.Count == 0)
                return;
            db.TrustedDevices.RemoveRange(devices);
            await db.SaveChangesAsync(cancellationToken);
        }
    }
}
