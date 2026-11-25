using DS.Platform.Domain.Entities.Billing;
using DS.Platform.Domain.Enums;

namespace DS.Platform.Application.Features.Subscriptions
{
    /// <summary>
    /// Estado de una suscripción (cálculo puro, fechas de Lima). Lo usan todos los commands que cambian fechas o la suspensión
    /// y el job diario, así que el estado guardado siempre sale de la misma regla:
    /// <list type="bullet">
    ///   <item>Cubierto hasta = el mayor entre el fin de la prueba y lo pagado; sin ninguno, el día anterior al inicio.</item>
    ///   <item>Cancelada: final, no cambia.</item>
    ///   <item>Suspendida a mano: suspendida aunque esté al día.</item>
    ///   <item>Pagado hasta ≥ hoy: Activa. Si no, prueba vigente: Prueba. Si no, hoy ≤ cubierto (inicio futuro): Activa.</item>
    ///   <item>Hasta cubierto + días de gracia: En gracia (entra con aviso). Después: Suspendida.</item>
    /// </list>
    /// </summary>
    public static class SubscriptionStatusCalculator
    {
        public static DateOnly CoveredUntil(Subscription s)
        {
            DateOnly? covered = (s.TrialEndsOn, s.PaidUntil) switch
            {
                ({ } trial, { } paid) => trial > paid ? trial : paid,
                ({ } trial, null) => trial,
                (null, { } paid) => paid,
                _ => null
            };
            return covered ?? s.StartOn.AddDays(-1);
        }

        public static SubscriptionStatus Evaluate(Subscription s, DateOnly today)
        {
            if (s.Status == SubscriptionStatus.Cancelled || s.CancelledOn is not null)
            {
                return SubscriptionStatus.Cancelled;
            }
            if (s.SuspendedManually)
            {
                return SubscriptionStatus.Suspended;
            }
            if (s.PaidUntil is { } paid && paid >= today)
            {
                return SubscriptionStatus.Active;
            }
            if (s.TrialEndsOn is { } trial && trial >= today)
            {
                return SubscriptionStatus.Trial;
            }

            var covered = CoveredUntil(s);
            if (today <= covered)
            {
                return SubscriptionStatus.Active;
            }
            return today <= covered.AddDays(s.GraceDays) ? SubscriptionStatus.Grace : SubscriptionStatus.Suspended;
        }

        /// <summary>Recalcula cubierto hasta, fecha de suspensión y estado. Devuelve true si algo cambió.</summary>
        public static bool Apply(Subscription s, DateOnly today)
        {
            var before = (s.CoveredUntil, s.SuspendOn, s.Status);

            s.CoveredUntil = CoveredUntil(s);
            s.Status = Evaluate(s, today);
            s.SuspendOn = s.Status == SubscriptionStatus.Cancelled ? null : s.CoveredUntil.AddDays(s.GraceDays + 1);

            return before != (s.CoveredUntil, s.SuspendOn, s.Status);
        }

        /// <summary>Nombre del estado para mostrar.</summary>
        public static string StatusName(SubscriptionStatus status) => status switch
        {
            SubscriptionStatus.Trial => "Prueba",
            SubscriptionStatus.Active => "Activa",
            SubscriptionStatus.Grace => "En gracia",
            SubscriptionStatus.Suspended => "Suspendida",
            SubscriptionStatus.Cancelled => "Cancelada",
            _ => status.ToString()
        };
    }
}
