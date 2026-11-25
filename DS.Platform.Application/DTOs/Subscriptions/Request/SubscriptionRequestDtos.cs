using DS.Platform.Domain.Enums;

namespace DS.Platform.Application.DTOs.Subscriptions.Request
{
    /// <summary>Alta de una suscripción: el producto, la moneda y la prueba salen del plan.</summary>
    public class CreateSubscriptionRequestDto
    {
        public required int CustomerId { get; set; }
        public required int PlanId { get; set; }
        public BillingCycle BillingCycle { get; set; } = BillingCycle.Monthly;
        /// <summary>Inicio (fecha de Lima); null = hoy.</summary>
        public DateOnly? StartOn { get; set; }
        /// <summary>Precio pactado por ciclo; null = el del plan para ese ciclo.</summary>
        public decimal? Price { get; set; }
        /// <summary>Día de vencimiento (1-28); null = el día del inicio (como mucho 28).</summary>
        public int? BillingDay { get; set; }
        public int GraceDays { get; set; } = 5;
        public string? Notes { get; set; }
    }

    /// <summary>Edición: cambiar de plan (del mismo producto), ciclo, precio, día de vencimiento, gracia y notas.</summary>
    public class UpdateSubscriptionRequestDto
    {
        public required int Id { get; set; }
        public required int PlanId { get; set; }
        public BillingCycle BillingCycle { get; set; } = BillingCycle.Monthly;
        public required decimal Price { get; set; }
        public required int BillingDay { get; set; }
        public int GraceDays { get; set; } = 5;
        public string? Notes { get; set; }
    }

    /// <summary>Prórroga manual: nueva fecha de "pagado hasta" con motivo (no reemplaza a un pago).</summary>
    public class ExtendSubscriptionRequestDto
    {
        public required int Id { get; set; }
        public required DateOnly PaidUntil { get; set; }
        public required string Reason { get; set; }
    }

    /// <summary>Suspender, reactivar o cancelar: siempre con motivo (queda en la suscripción y en la auditoría).</summary>
    public class SubscriptionReasonRequestDto
    {
        public required int Id { get; set; }
        public required string Reason { get; set; }
    }
}
