namespace DS.Platform.Domain.Enums
{
    /// <summary>Ciclo de facturación de una suscripción.</summary>
    public enum BillingCycle
    {
        Monthly = 1,
        Yearly = 2
    }

    /// <summary>
    /// Estado de una suscripción (fechas de Lima). Lo calcula <c>SubscriptionStatusCalculator</c> en cada cambio y en el job
    /// diario: Prueba → Activa (hoy ≤ pagado hasta) → En gracia (hasta "cubierto hasta + días de gracia") → Suspendida →
    /// Cancelada (final).
    /// </summary>
    public enum SubscriptionStatus
    {
        Trial = 1,
        Active = 2,
        Grace = 3,
        Suspended = 4,
        Cancelled = 5
    }
}
