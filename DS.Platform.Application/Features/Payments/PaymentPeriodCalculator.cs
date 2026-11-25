using DS.Platform.Application.Features.Subscriptions;
using DS.Platform.Domain.Entities.Billing;
using DS.Platform.Domain.Enums;

namespace DS.Platform.Application.Features.Payments
{
    /// <summary>
    /// Periodo que cubre un pago (cálculo puro, fechas de Lima):
    /// <list type="bullet">
    ///   <item>Empieza el día siguiente a "cubierto hasta" (sin huecos: los días de gracia usados se pagan).</item>
    ///   <item>Si la suscripción estuvo suspendida más allá de la gracia (el inicio quedaría antes de hoy − gracia), empieza
    ///   hoy: no se cobran los meses de corte.</item>
    ///   <item>Termina tras <c>ciclos × (1 mes o 12 meses)</c>, el día anterior.</item>
    /// </list>
    /// </summary>
    public static class PaymentPeriodCalculator
    {
        public static (DateOnly Start, DateOnly End) For(Subscription subscription, int periods, DateOnly today)
        {
            var start = SubscriptionStatusCalculator.CoveredUntil(subscription).AddDays(1);
            if (start < today.AddDays(-subscription.GraceDays))
            {
                start = today;
            }

            var months = periods * (subscription.BillingCycle == BillingCycle.Yearly ? 12 : 1);
            return (start, start.AddMonths(months).AddDays(-1));
        }

        public static decimal ExpectedAmount(Subscription subscription, int periods) => subscription.Price * periods;

        public static string MethodName(PaymentMethod method) => method switch
        {
            PaymentMethod.Transfer => "Transferencia",
            PaymentMethod.Deposit => "Depósito",
            PaymentMethod.Yape => "Yape",
            PaymentMethod.Plin => "Plin",
            PaymentMethod.Cash => "Efectivo",
            PaymentMethod.Card => "Tarjeta",
            _ => "Otro"
        };
    }
}
