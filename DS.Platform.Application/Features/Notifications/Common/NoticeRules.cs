using DS.Platform.Domain.Entities.Billing;
using DS.Platform.Domain.Enums;

namespace DS.Platform.Application.Features.Notifications.Common
{
    /// <summary>
    /// Regla pura: ¿qué aviso de cobro toca hoy a una suscripción? Como mucho uno (el más urgente). El día se cuenta contra "cubierto
    /// hasta" (último día cubierto, o fin de la prueba): faltan 1 a 5 días → por vencer; hoy es ese día → vence hoy; pasó y está En
    /// gracia → gracia; Suspendida por falta de pago → suspensión. La suspensión manual no avisa (la decidió soporte, con motivo) y la
    /// cancelada tampoco. La clave lleva "cubierto hasta": al pagar cambia y el siguiente periodo vuelve a avisar, pero el mismo
    /// periodo nunca repite el mismo aviso.
    /// </summary>
    public static class NoticeRules
    {
        /// <summary>Con cuántos días de anticipación se avisa que vence.</summary>
        public const int DueSoonDays = 5;

        public sealed record Due(NotificationType Type, string DedupKey);

        public static Due? For(Subscription subscription, DateOnly today)
        {
            if (subscription.IsDeleted)
            {
                return null;
            }
            var covered = subscription.CoveredUntil.ToString("yyyyMMdd");
            switch (subscription.Status)
            {
                case SubscriptionStatus.Suspended when !subscription.SuspendedManually:
                    return new Due(NotificationType.Suspended, $"suspended:{subscription.Id}:{covered}");
                case SubscriptionStatus.Grace:
                    return new Due(NotificationType.GraceEntered, $"grace:{subscription.Id}:{covered}");
                case SubscriptionStatus.Trial or SubscriptionStatus.Active:
                    var days = subscription.CoveredUntil.DayNumber - today.DayNumber;
                    if (days == 0)
                    {
                        return new Due(NotificationType.DueToday, $"due-today:{subscription.Id}:{covered}");
                    }
                    return days is >= 1 and <= DueSoonDays ? new Due(NotificationType.DueSoon, $"due-soon:{subscription.Id}:{covered}") : null;
                default:
                    return null;
            }
        }

        /// <summary>Días de aviso del certificado: se avisa al entrar en cada tramo (30, 7 y 1 día) y cuando ya venció (tramo 0).</summary>
        public static readonly int[] CertificateBuckets = [1, 7, 30];

        /// <summary>Tramo del certificado según los días que le quedan (negativo = vencido), o null si todavía está lejos.</summary>
        public static int? CertificateBucket(long daysLeft)
        {
            if (daysLeft < 0)
            {
                return 0;
            }
            foreach (var bucket in CertificateBuckets)
            {
                if (daysLeft <= bucket)
                {
                    return bucket;
                }
            }
            return null;
        }

        public static string CertificateKey(long tenantId, DateOnly expiresOn, int bucket) => $"cert:{tenantId}:{expiresOn:yyyyMMdd}:{bucket}";
    }
}
