using DS.Platform.Domain.Enums;

namespace DS.Platform.Application.DTOs.Payments.Request
{
    /// <summary>Registrar un pago: el periodo que cubre lo calcula el servidor (no viene del formulario).</summary>
    public class RegisterPaymentRequestDto
    {
        public required int SubscriptionId { get; set; }
        /// <summary>Fecha del pago (Lima); null = hoy. No puede ser futura.</summary>
        public DateOnly? PaidOn { get; set; }
        public required decimal Amount { get; set; }
        /// <summary>Debe ser la moneda de la suscripción.</summary>
        public string Currency { get; set; } = "PEN";
        public required PaymentMethod Method { get; set; }
        public string? OperationNumber { get; set; }
        /// <summary>Ciclos que paga (meses o años según la suscripción), 1 a 24.</summary>
        public int Periods { get; set; } = 1;
        public string? Notes { get; set; }
    }

    public class VoidPaymentRequestDto
    {
        public required int Id { get; set; }
        public required string Reason { get; set; }
    }
}
