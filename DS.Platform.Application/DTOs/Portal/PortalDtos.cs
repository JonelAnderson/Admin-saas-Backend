using DS.Platform.Domain.Enums;

namespace DS.Platform.Application.DTOs.Portal.Request
{
    /// <summary>Invita (o reinvita) a una persona al portal de clientes con acceso a empresas emisoras de un cliente.</summary>
    public class InviteToPortalRequestDto
    {
        public required int CustomerId { get; set; }
        public required string Email { get; set; }
        public required string Name { get; set; }
        /// <summary>Ids de <c>ApiSunatIssuer</c> (empresas emisoras vinculadas al cliente) a las que entra.</summary>
        public required List<int> IssuerIds { get; set; }
        /// <summary>Borra también el MFA de la persona (perdió su teléfono).</summary>
        public bool ResetMfa { get; set; }
    }
}

namespace DS.Platform.Application.DTOs.Portal.Response
{
    public class PortalIssuerDto
    {
        public int Id { get; set; }
        public string Ruc { get; set; } = null!;
        public string BusinessName { get; set; } = null!;
    }

    /// <summary>Fila del listado de accesos al portal. Nunca trae el token de activación.</summary>
    public class PortalAccessResponseDto
    {
        public int Id { get; set; }
        public int CustomerId { get; set; }
        public string CustomerName { get; set; } = null!;
        public string CustomerRuc { get; set; } = null!;
        public string Email { get; set; } = null!;
        public string Name { get; set; } = null!;
        public bool IsEnabled { get; set; }
        public bool ResetMfa { get; set; }
        /// <summary>La API ya creó al usuario (la primera invitación se cumplió).</summary>
        public bool Invited { get; set; }
        public DateTime? InvitedOnUtc { get; set; }
        public DateTime? InvitationExpiresOnUtc { get; set; }
        public PortalWorkType WorkType { get; set; }
        public string WorkTypeName { get; set; } = null!;
        public RemoteActionStatus Status { get; set; }
        public string StatusName { get; set; } = null!;
        public int Attempts { get; set; }
        public DateTime NextAttemptOnUtc { get; set; }
        public string? LastError { get; set; }
        public DateTime? DoneOnUtc { get; set; }
        public DateTime CreatedOn { get; set; }
        public List<PortalIssuerDto> Issuers { get; set; } = new();
    }
}
