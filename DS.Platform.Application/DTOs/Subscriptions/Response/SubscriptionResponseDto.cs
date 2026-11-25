using DS.Platform.Domain.Enums;

namespace DS.Platform.Application.DTOs.Subscriptions.Response
{
    public class SubscriptionResponseDto
    {
        public int Id { get; set; }
        public int CustomerId { get; set; }
        public string CustomerRuc { get; set; } = null!;
        public string CustomerName { get; set; } = null!;
        public int PlanId { get; set; }
        public string PlanCode { get; set; } = null!;
        public string PlanName { get; set; } = null!;
        public PlanProduct Product { get; set; }
        public string ProductName { get; set; } = null!;
        public BillingCycle BillingCycle { get; set; }
        public string BillingCycleName { get; set; } = null!;
        public decimal Price { get; set; }
        public string Currency { get; set; } = null!;
        public DateOnly StartOn { get; set; }
        public DateOnly? TrialEndsOn { get; set; }
        public DateOnly? PaidUntil { get; set; }
        public int BillingDay { get; set; }
        public int GraceDays { get; set; }
        public DateOnly CoveredUntil { get; set; }
        public DateOnly? SuspendOn { get; set; }
        public SubscriptionStatus Status { get; set; }
        public string StatusName { get; set; } = null!;
        public bool SuspendedManually { get; set; }
        public string? StatusReason { get; set; }
        public DateOnly? CancelledOn { get; set; }
        public string? Notes { get; set; }
    }
}
