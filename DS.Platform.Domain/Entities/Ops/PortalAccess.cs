using DS.Platform.Domain.Contracts;
using DS.Platform.Domain.Entities.ApiSunat;
using DS.Platform.Domain.Entities.Customers;
using DS.Platform.Domain.Enums;
using System;
using System.Collections.Generic;

namespace DS.Platform.Domain.Entities.Ops
{
    /// <summary>
    /// Acceso de una persona al portal de clientes de la API SUNAT (fase C6). La consola es dueña de quién entra y a qué empresas:
    /// la API SUNAT guarda el usuario real (contraseña, MFA, sesiones). Igual que <see cref="InstanceDeployment"/>, la llamada a la API
    /// nunca va dentro del caso de uso: la fila lleva su propio trabajo pendiente (invitar o habilitar / deshabilitar) con reintentos
    /// y el job <c>PortalAccessDispatch</c> lo ejecuta. Usa los mismos estados que <see cref="RemoteActionStatus"/>.
    /// Un correo pertenece a un solo cliente.
    /// </summary>
    public class PortalAccess : AuditableEntity<int>, IConcurrencyChecked
    {
        public required int CustomerId { get; set; }
        /// <summary>Correo de la persona, en minúsculas y sin espacios. Es su usuario en el portal; único entre los accesos.</summary>
        public required string Email { get; set; }
        public required string Name { get; set; }

        /// <summary>Lo que la consola quiere: la persona puede entrar. Deshabilitar cierra sus sesiones en la API.</summary>
        public bool IsEnabled { get; set; } = true;
        /// <summary>La próxima invitación también borra el MFA de la persona (perdió su teléfono).</summary>
        public bool ResetMfa { get; set; }

        /// <summary>Id del usuario en la API SUNAT; null hasta que la primera invitación se cumple.</summary>
        public long? ApiUserId { get; set; }
        public DateTime? InvitedOnUtc { get; set; }
        public DateTime? InvitationExpiresOnUtc { get; set; }
        /// <summary>Invitaciones cumplidas: parte de la clave del aviso, para que cada una tenga el suyo.</summary>
        public int InvitationCount { get; set; }

        // Trabajo pendiente (uno a la vez: el último pedido reemplaza al anterior).
        public PortalWorkType WorkType { get; set; } = PortalWorkType.Invite;
        public RemoteActionStatus Status { get; set; } = RemoteActionStatus.Pending;
        public int Attempts { get; set; }
        public DateTime NextAttemptOnUtc { get; set; }
        /// <summary>Mientras está En curso: hasta cuándo es suyo (si el proceso murió, otro lo retoma pasado ese momento).</summary>
        public DateTime? LockedUntilUtc { get; set; }
        public string? LastError { get; set; }
        public DateTime? DoneOnUtc { get; set; }

        public virtual Customer? Customer { get; set; }
        /// <summary>Empresas emisoras a las que entra. Se reemplazan completas al invitar.</summary>
        public virtual ICollection<PortalAccessIssuer> Issuers { get; set; } = new List<PortalAccessIssuer>();
    }

    /// <summary>Empresa emisora a la que da acceso un <see cref="PortalAccess"/> (clave compuesta, sin auditoría propia).</summary>
    public class PortalAccessIssuer
    {
        public int PortalAccessId { get; set; }
        public int IssuerId { get; set; }

        public virtual PortalAccess? PortalAccess { get; set; }
        public virtual ApiSunatIssuer? Issuer { get; set; }
    }
}
