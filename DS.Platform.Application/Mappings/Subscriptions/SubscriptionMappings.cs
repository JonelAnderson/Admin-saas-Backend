using System.Linq.Expressions;
using DS.Platform.Application.DTOs.Subscriptions.Response;
using DS.Platform.Application.Mappings.Plans;
using DS.Platform.Domain.Entities.Billing;
using DS.Platform.Domain.Enums;

namespace DS.Platform.Application.Mappings.Subscriptions
{
    /// <summary>
    /// Mapeo explícito de suscripciones (sin AutoMapper): proyección que EF traduce a SQL, con el cliente y el plan por JOIN.
    /// Las altas y cambios los arman los handlers (las fechas y el estado salen de reglas, no del formulario).
    /// </summary>
    public static class SubscriptionMappings
    {
        public static readonly Expression<Func<Subscription, SubscriptionResponseDto>> ToResponse = s => new SubscriptionResponseDto
        {
            Id = s.Id,
            CustomerId = s.CustomerId,
            CustomerRuc = s.Customer != null ? s.Customer.Ruc : string.Empty,
            CustomerName = s.Customer != null ? s.Customer.BusinessName : string.Empty,
            PlanId = s.PlanId,
            PlanCode = s.Plan != null ? s.Plan.Code : string.Empty,
            PlanName = s.Plan != null ? s.Plan.Name : string.Empty,
            Product = s.Product,
            ProductName = s.Product == PlanProduct.Erp ? PlanMappings.ErpName : PlanMappings.ApiSunatName,
            BillingCycle = s.BillingCycle,
            BillingCycleName = s.BillingCycle == BillingCycle.Yearly ? "Anual" : "Mensual",
            Price = s.Price,
            Currency = s.Currency,
            StartOn = s.StartOn,
            TrialEndsOn = s.TrialEndsOn,
            PaidUntil = s.PaidUntil,
            BillingDay = s.BillingDay,
            GraceDays = s.GraceDays,
            CoveredUntil = s.CoveredUntil,
            SuspendOn = s.SuspendOn,
            Status = s.Status,
            StatusName = s.Status == SubscriptionStatus.Trial ? "Prueba"
                : s.Status == SubscriptionStatus.Active ? "Activa"
                : s.Status == SubscriptionStatus.Grace ? "En gracia"
                : s.Status == SubscriptionStatus.Suspended ? "Suspendida"
                : "Cancelada",
            SuspendedManually = s.SuspendedManually,
            StatusReason = s.StatusReason,
            CancelledOn = s.CancelledOn,
            Notes = s.Notes
        };
    }
}
