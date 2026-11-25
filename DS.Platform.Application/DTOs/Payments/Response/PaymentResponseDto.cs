using DS.Platform.Domain.Enums;

namespace DS.Platform.Application.DTOs.Payments.Response
{
    public class PaymentResponseDto
    {
        public int Id { get; set; }
        public int SubscriptionId { get; set; }
        public int CustomerId { get; set; }
        public string CustomerRuc { get; set; } = null!;
        public string CustomerName { get; set; } = null!;
        public string PlanName { get; set; } = null!;
        public PlanProduct Product { get; set; }
        public string ProductName { get; set; } = null!;
        public DateOnly PaidOn { get; set; }
        public decimal Amount { get; set; }
        public decimal ExpectedAmount { get; set; }
        public string Currency { get; set; } = null!;
        public PaymentMethod Method { get; set; }
        public string MethodName { get; set; } = null!;
        public string? OperationNumber { get; set; }
        public int Periods { get; set; }
        public DateOnly PeriodStart { get; set; }
        public DateOnly PeriodEnd { get; set; }
        public PaymentStatus Status { get; set; }
        public string StatusName { get; set; } = null!;
        public DateOnly? VoidedOn { get; set; }
        public string? VoidReason { get; set; }
        public string? Notes { get; set; }
        /// <summary>Usuario que lo registró (id; el nombre lo resuelve la pantalla de usuarios / auditoría).</summary>
        public string CreatedBy { get; set; } = null!;
    }
}
