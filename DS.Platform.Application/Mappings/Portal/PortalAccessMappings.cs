using System.Linq.Expressions;
using DS.Platform.Application.DTOs.Portal.Response;
using DS.Platform.Domain.Entities.Ops;
using DS.Platform.Domain.Enums;

namespace DS.Platform.Application.Mappings.Portal
{
    /// <summary>Mapeo explícito del listado de accesos al portal: proyección con el cliente y las empresas por JOIN.</summary>
    public static class PortalAccessMappings
    {
        public static readonly Expression<Func<PortalAccess, PortalAccessResponseDto>> ToResponse = a => new PortalAccessResponseDto
        {
            Id = a.Id,
            CustomerId = a.CustomerId,
            CustomerName = a.Customer != null ? a.Customer.BusinessName : string.Empty,
            CustomerRuc = a.Customer != null ? a.Customer.Ruc : string.Empty,
            Email = a.Email,
            Name = a.Name,
            IsEnabled = a.IsEnabled,
            ResetMfa = a.ResetMfa,
            Invited = a.ApiUserId != null,
            InvitedOnUtc = a.InvitedOnUtc,
            InvitationExpiresOnUtc = a.InvitationExpiresOnUtc,
            WorkType = a.WorkType,
            WorkTypeName = a.WorkType == PortalWorkType.Invite ? "Invitar" : a.IsEnabled ? "Habilitar" : "Deshabilitar",
            Status = a.Status,
            StatusName = a.Status == RemoteActionStatus.Pending ? "Pendiente"
                : a.Status == RemoteActionStatus.Sending ? "En curso"
                : a.Status == RemoteActionStatus.Done ? "Hecho"
                : a.Status == RemoteActionStatus.Failed ? "Fallido (se reintenta)"
                : a.Status == RemoteActionStatus.Abandoned ? "Abandonado"
                : "Cancelado",
            Attempts = a.Attempts,
            NextAttemptOnUtc = a.NextAttemptOnUtc,
            LastError = a.LastError,
            DoneOnUtc = a.DoneOnUtc,
            CreatedOn = a.CreatedOn,
            Issuers = a.Issuers
                .Select(i => new PortalIssuerDto { Id = i.IssuerId, Ruc = i.Issuer!.Ruc, BusinessName = i.Issuer.BusinessName })
                .ToList()
        };
    }
}
