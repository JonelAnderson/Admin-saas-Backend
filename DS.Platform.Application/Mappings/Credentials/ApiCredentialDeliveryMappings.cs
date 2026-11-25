using System.Linq.Expressions;
using DS.Platform.Application.DTOs.Credentials.Response;
using DS.Platform.Domain.Entities.Ops;
using DS.Platform.Domain.Enums;

namespace DS.Platform.Application.Mappings.Credentials
{
    /// <summary>Mapeo explícito del listado de entregas de credenciales: proyección con cliente, instancia y emisor por JOIN. Nunca proyecta el secreto.</summary>
    public static class ApiCredentialDeliveryMappings
    {
        public static readonly Expression<Func<ApiCredentialDelivery, ApiCredentialDeliveryResponseDto>> ToResponse = d => new ApiCredentialDeliveryResponseDto
        {
            Id = d.Id,
            CustomerId = d.CustomerId,
            CustomerName = d.Customer != null ? d.Customer.BusinessName : string.Empty,
            InstanceId = d.InstanceId,
            InstanceName = d.Instance != null ? d.Instance.Name : string.Empty,
            IssuerId = d.IssuerId,
            IssuerRuc = d.Ruc,
            IssuerName = d.Issuer != null ? d.Issuer.BusinessName : string.Empty,
            Status = d.Status,
            StatusName = d.Status == ApiCredentialDeliveryStatus.Pending ? "Pendiente"
                : d.Status == ApiCredentialDeliveryStatus.Sending ? "En curso"
                : d.Status == ApiCredentialDeliveryStatus.Ready ? "Lista (esperando al ERP)"
                : d.Status == ApiCredentialDeliveryStatus.Delivered ? "Entregada"
                : d.Status == ApiCredentialDeliveryStatus.Failed ? "Fallida (se reintenta)"
                : d.Status == ApiCredentialDeliveryStatus.Abandoned ? "Abandonada"
                : d.Status == ApiCredentialDeliveryStatus.Cancelled ? "Cancelada"
                : "Vencida",
            Attempts = d.Attempts,
            NextAttemptOnUtc = d.NextAttemptOnUtc,
            LastError = d.LastError,
            ReadyOnUtc = d.ReadyOnUtc,
            ExpiresOnUtc = d.ExpiresOnUtc,
            LastPulledOnUtc = d.LastPulledOnUtc,
            DeliveredOnUtc = d.DeliveredOnUtc,
            WaitingForErp = d.Status == ApiCredentialDeliveryStatus.Ready && d.LastPulledOnUtc == null,
            CreatedOn = d.CreatedOn
        };
    }
}
