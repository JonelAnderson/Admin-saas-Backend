using Microsoft.AspNetCore.DataProtection;
using System.Text.Json;

namespace DS.Platform.Infrastructure.Services.Identity
{
    /// <summary>
    /// Ticket opaco entre los pasos del login (Fase 12a): id del usuario + security stamp + etapa, cifrado con Data Protection
    /// de tiempo limitado (5 minutos). No es un JWT, así que el bearer de la API no lo acepta. Deja de servir si cambia
    /// el security stamp (contraseña cambiada, MFA restablecido) y un ticket de una etapa no sirve para la otra.
    /// </summary>
    public sealed class MfaTicketProtector
    {
        public const string Purpose = "Identity.MfaTicket";
        public const string StageVerify = "verify";
        public const string StageSetup = "setup";
        public static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(5);

        private readonly ITimeLimitedDataProtector _protector;

        public MfaTicketProtector(IDataProtectionProvider provider)
        {
            _protector = provider.CreateProtector(Purpose).ToTimeLimitedDataProtector();
        }

        public string Create(string userId, string securityStamp, string stage, DateTimeOffset? expiresAt = null)
            => _protector.Protect(
                JsonSerializer.Serialize(new MfaTicket(userId, securityStamp, stage)),
                expiresAt ?? DateTimeOffset.UtcNow.Add(Lifetime));

        /// <summary>null si el ticket venció, fue alterado o no es de este sistema.</summary>
        public MfaTicket? Read(string? ticket)
        {
            if (string.IsNullOrWhiteSpace(ticket))
            {
                return null;
            }

            try
            {
                var payload = JsonSerializer.Deserialize<MfaTicket>(_protector.Unprotect(ticket.Trim(), out _));
                return payload is null || string.IsNullOrEmpty(payload.UserId) || string.IsNullOrEmpty(payload.Stage) ? null : payload;
            }
            catch (Exception)
            {
                // CryptographicException (vencido / alterado / otra clave), FormatException (no es base64url), JsonException.
                return null;
            }
        }
    }

    public sealed record MfaTicket(string UserId, string SecurityStamp, string Stage);
}
