using DS.Platform.Domain.Enums;

namespace DS.Platform.Application.DTOs.Notifications.Response
{
    /// <summary>Fila de la bandeja de avisos por correo (sin el cuerpo: solo asunto y estado).</summary>
    public class NotificationResponseDto
    {
        public int Id { get; set; }
        public NotificationType Type { get; set; }
        public string TypeName { get; set; } = null!;
        public int CustomerId { get; set; }
        public string CustomerName { get; set; } = null!;
        public string? ToEmail { get; set; }
        public string Subject { get; set; } = null!;
        public NotificationStatus Status { get; set; }
        public string StatusName { get; set; } = null!;
        public int Attempts { get; set; }
        public DateTime NextAttemptOnUtc { get; set; }
        public string? LastError { get; set; }
        public DateTime? SentOnUtc { get; set; }
        public DateTime CreatedOn { get; set; }
    }
}
