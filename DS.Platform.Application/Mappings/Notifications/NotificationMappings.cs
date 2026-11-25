using System.Linq.Expressions;
using DS.Platform.Application.DTOs.Notifications.Response;
using DS.Platform.Domain.Entities.Ops;
using DS.Platform.Domain.Enums;

namespace DS.Platform.Application.Mappings.Notifications
{
    /// <summary>Mapeo explícito de la bandeja de avisos: proyección con el cliente por JOIN.</summary>
    public static class NotificationMappings
    {
        public static readonly Expression<Func<Notification, NotificationResponseDto>> ToResponse = n => new NotificationResponseDto
        {
            Id = n.Id,
            Type = n.Type,
            TypeName = n.Type == NotificationType.DueSoon ? "Por vencer"
                : n.Type == NotificationType.DueToday ? "Vence hoy"
                : n.Type == NotificationType.GraceEntered ? "En gracia"
                : n.Type == NotificationType.Suspended ? "Suspensión"
                : n.Type == NotificationType.PaymentRegistered ? "Pago registrado"
                : n.Type == NotificationType.PortalInvitation ? "Invitación al portal"
                : "Certificado",
            CustomerId = n.CustomerId,
            CustomerName = n.Customer != null ? n.Customer.BusinessName : string.Empty,
            ToEmail = n.ToEmail,
            Subject = n.Subject,
            Status = n.Status,
            StatusName = n.Status == NotificationStatus.Pending ? "Pendiente"
                : n.Status == NotificationStatus.Sending ? "Enviando"
                : n.Status == NotificationStatus.Sent ? "Enviado"
                : n.Status == NotificationStatus.Failed ? "Fallido (se reintenta)"
                : n.Status == NotificationStatus.Abandoned ? "Abandonado"
                : "Omitido",
            Attempts = n.Attempts,
            NextAttemptOnUtc = n.NextAttemptOnUtc,
            LastError = n.LastError,
            SentOnUtc = n.SentOnUtc,
            CreatedOn = n.CreatedOn
        };
    }
}
