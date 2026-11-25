using System.Linq.Expressions;
using DS.Platform.Application.DTOs.Payments.Response;
using DS.Platform.Application.Mappings.Plans;
using DS.Platform.Domain.Entities.Billing;
using DS.Platform.Domain.Enums;

namespace DS.Platform.Application.Mappings.Payments
{
    /// <summary>Mapeo explícito de pagos (sin AutoMapper): proyección que EF traduce a SQL con cliente y plan por JOIN.</summary>
    public static class PaymentMappings
    {
        public static readonly Expression<Func<Payment, PaymentResponseDto>> ToResponse = p => new PaymentResponseDto
        {
            Id = p.Id,
            SubscriptionId = p.SubscriptionId,
            CustomerId = p.CustomerId,
            CustomerRuc = p.Customer != null ? p.Customer.Ruc : string.Empty,
            CustomerName = p.Customer != null ? p.Customer.BusinessName : string.Empty,
            PlanName = p.Subscription != null && p.Subscription.Plan != null ? p.Subscription.Plan.Name : string.Empty,
            Product = p.Subscription != null ? p.Subscription.Product : PlanProduct.Erp,
            ProductName = p.Subscription != null && p.Subscription.Product == PlanProduct.ApiSunat ? PlanMappings.ApiSunatName : PlanMappings.ErpName,
            PaidOn = p.PaidOn,
            Amount = p.Amount,
            ExpectedAmount = p.ExpectedAmount,
            Currency = p.Currency,
            Method = p.Method,
            MethodName = p.Method == PaymentMethod.Transfer ? "Transferencia"
                : p.Method == PaymentMethod.Deposit ? "Depósito"
                : p.Method == PaymentMethod.Yape ? "Yape"
                : p.Method == PaymentMethod.Plin ? "Plin"
                : p.Method == PaymentMethod.Cash ? "Efectivo"
                : p.Method == PaymentMethod.Card ? "Tarjeta"
                : "Otro",
            OperationNumber = p.OperationNumber,
            Periods = p.Periods,
            PeriodStart = p.PeriodStart,
            PeriodEnd = p.PeriodEnd,
            Status = p.Status,
            StatusName = p.Status == PaymentStatus.Voided ? "Anulado" : "Registrado",
            VoidedOn = p.VoidedOn,
            VoidReason = p.VoidReason,
            Notes = p.Notes,
            CreatedBy = p.CreatedBy
        };
    }
}
