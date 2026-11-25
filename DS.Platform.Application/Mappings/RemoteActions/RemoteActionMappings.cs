using System.Linq.Expressions;
using DS.Platform.Application.DTOs.RemoteActions.Response;
using DS.Platform.Domain.Entities.Ops;
using DS.Platform.Domain.Enums;

namespace DS.Platform.Application.Mappings.RemoteActions
{
    /// <summary>Mapeo explícito de la bandeja de acciones remotas: proyección con el emisor y el cliente por JOIN.</summary>
    public static class RemoteActionMappings
    {
        public static readonly Expression<Func<RemoteAction, RemoteActionResponseDto>> ToResponse = a => new RemoteActionResponseDto
        {
            Id = a.Id,
            Type = a.Type,
            TypeName = a.Type == RemoteActionType.SuspendIssuer ? "Suspender emisor"
                : a.Type == RemoteActionType.ReactivateIssuer ? "Reactivar emisor" : "Aplicar plan",
            IssuerId = a.IssuerId,
            IssuerRuc = a.Issuer != null ? a.Issuer.Ruc : string.Empty,
            IssuerName = a.Issuer != null ? a.Issuer.BusinessName : string.Empty,
            CustomerId = a.CustomerId,
            CustomerName = a.Issuer != null && a.Issuer.Customer != null ? a.Issuer.Customer.BusinessName : string.Empty,
            Reason = a.Reason,
            Actor = a.CreatedBy != "" ? a.CreatedBy : "sistema",
            Status = a.Status,
            StatusName = a.Status == RemoteActionStatus.Pending ? "Pendiente"
                : a.Status == RemoteActionStatus.Sending ? "Enviando"
                : a.Status == RemoteActionStatus.Done ? "Hecha"
                : a.Status == RemoteActionStatus.Failed ? "Fallida (se reintenta)"
                : a.Status == RemoteActionStatus.Abandoned ? "Abandonada"
                : "Cancelada",
            Attempts = a.Attempts,
            NextAttemptOnUtc = a.NextAttemptOnUtc,
            LastError = a.LastError,
            DoneOnUtc = a.DoneOnUtc,
            CreatedOn = a.CreatedOn
        };
    }
}
