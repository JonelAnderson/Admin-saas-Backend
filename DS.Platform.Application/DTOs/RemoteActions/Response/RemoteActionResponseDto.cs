using DS.Platform.Domain.Enums;

namespace DS.Platform.Application.DTOs.RemoteActions.Response
{
    /// <summary>Fila de la bandeja de acciones remotas.</summary>
    public class RemoteActionResponseDto
    {
        public int Id { get; set; }
        public RemoteActionType Type { get; set; }
        public string TypeName { get; set; } = null!;
        public int IssuerId { get; set; }
        public string IssuerRuc { get; set; } = null!;
        public string IssuerName { get; set; } = null!;
        public int CustomerId { get; set; }
        public string CustomerName { get; set; } = null!;
        public string Reason { get; set; } = null!;
        /// <summary>Usuario de la consola que la originó; "sistema" si fue el job diario.</summary>
        public string Actor { get; set; } = null!;
        public RemoteActionStatus Status { get; set; }
        public string StatusName { get; set; } = null!;
        public int Attempts { get; set; }
        public DateTime NextAttemptOnUtc { get; set; }
        public string? LastError { get; set; }
        public DateTime? DoneOnUtc { get; set; }
        public DateTime CreatedOn { get; set; }
    }
}
