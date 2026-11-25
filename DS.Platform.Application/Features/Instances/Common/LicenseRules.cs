using System.Security.Cryptography;
using System.Text;
using DS.Platform.Domain.Entities.Billing;
using DS.Platform.Domain.Enums;

namespace DS.Platform.Application.Features.Instances.Common
{
    /// <summary>Lo que se le entrega a una instancia del ERP, derivado de la suscripción del ERP de su cliente.</summary>
    public sealed record LicenseState(
        LicenseStatus Status,
        DateOnly? CoveredUntil,
        DateOnly? SuspendOn,
        DateOnly? TrialEndsOn,
        string? PlanCode,
        int? MaxCompanies,
        int? MaxUsers,
        int? MaxBranches,
        bool Ai,
        string? Message);

    /// <summary>
    /// Regla pura del estado de la licencia. Una suscripción de ERP no cancelada manda; si solo hay canceladas, la licencia está
    /// Cancelada; si el cliente nunca tuvo suscripción del ERP queda Suspendida (nada se registra hasta contratar un plan). Prueba
    /// y Activa son licencia Activa. Los límites del plan en null significan "sin límite".
    /// </summary>
    public static class LicenseStateBuilder
    {
        public const string SuspendedMessage = "El servicio está suspendido por falta de pago. Comuníquese con soporte para regularizarlo.";
        public const string CancelledMessage = "La suscripción fue cancelada. Comuníquese con soporte.";
        public const string NoSubscriptionMessage = "La instancia no tiene una suscripción del ERP vigente. Comuníquese con soporte.";

        /// <summary>Días antes del vencimiento desde los que se avisa.</summary>
        public const int WarnDays = 5;

        public static LicenseState Build(IReadOnlyCollection<Subscription> customerSubscriptions, DateOnly today)
        {
            var erp = customerSubscriptions.Where(s => !s.IsDeleted && s.Product == PlanProduct.Erp).ToList();
            var current = erp.Where(s => s.Status != SubscriptionStatus.Cancelled).OrderByDescending(s => s.Id).FirstOrDefault();
            if (current is null)
            {
                var cancelled = erp.OrderByDescending(s => s.Id).FirstOrDefault();
                return cancelled is null
                    ? new LicenseState(LicenseStatus.Suspended, null, null, null, null, 0, 0, 0, false, NoSubscriptionMessage)
                    : new LicenseState(LicenseStatus.Cancelled, cancelled.CoveredUntil, null, null, cancelled.Plan?.Code, 0, 0, 0, false, CancelledMessage);
            }

            var status = current.Status switch
            {
                SubscriptionStatus.Grace => LicenseStatus.Grace,
                SubscriptionStatus.Suspended => LicenseStatus.Suspended,
                _ => LicenseStatus.Active
            };
            var plan = current.Plan;
            var message = status switch
            {
                LicenseStatus.Suspended => SuspendedMessage,
                LicenseStatus.Grace => $"Su suscripción venció el {current.CoveredUntil:dd/MM/yyyy}. El servicio se suspenderá el "
                                       + $"{current.SuspendOn:dd/MM/yyyy} si no se regulariza el pago.",
                _ when current.CoveredUntil.DayNumber - today.DayNumber is >= 0 and <= WarnDays
                    => $"Su suscripción vence el {current.CoveredUntil:dd/MM/yyyy}.",
                _ => null
            };

            return new LicenseState(
                status, current.CoveredUntil, current.SuspendOn, current.TrialEndsOn, plan?.Code,
                plan?.MaxCompanies, plan?.MaxUsers, plan?.MaxBranches, plan?.IncludesAi ?? false, message);
        }
    }

    /// <summary>Claves de licencia: al azar, con prefijo; en la base solo el hash SHA-256.</summary>
    public static class LicenseKeys
    {
        public const string Prefix = "lic_";
        public const int MaxLength = 100;

        public static string Generate()
        {
            var bytes = RandomNumberGenerator.GetBytes(32);
            return Prefix + Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        }

        public static string Hash(string key) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key))).ToLowerInvariant();

        public static string Hint(string key) => key.Length <= 4 ? key : key[^4..];
    }

    /// <summary>Estado de contacto de una instancia: consulta cada 10 min, así que más de 30 es demora y más de un día es pérdida.</summary>
    public static class InstanceContact
    {
        public static readonly TimeSpan Online = TimeSpan.FromMinutes(30);
        public static readonly TimeSpan Delayed = TimeSpan.FromHours(24);

        public static (string Name, string Level) Describe(DateTime? lastContactUtc, DateTime nowUtc)
        {
            if (lastContactUtc is null)
            {
                return ("Nunca se conectó", "never");
            }
            var age = nowUtc - lastContactUtc.Value;
            return age <= Online ? ("En línea", "online") : age <= Delayed ? ("Con demora", "delayed") : ("Sin contacto", "lost");
        }
    }
}
