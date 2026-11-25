using DS.Platform.Domain.Contracts;
using DS.Platform.Domain.Entities.Erp;
using DS.Platform.Domain.Enums;
using System;

namespace DS.Platform.Domain.Entities.Ops
{
    /// <summary>
    /// Despliegue de una instancia del ERP en Coolify (fase C5). Igual que la bandeja de acciones remotas, la llamada a Coolify nunca
    /// va dentro del caso de uso: el handler guarda esta fila y el job <c>DeploymentsDispatch</c> la ejecuta con reintentos. Usa los
    /// mismos estados que <see cref="RemoteActionStatus"/> (Pendiente → Enviando → Hecha / Fallida / Abandonada / Cancelada).
    /// Como mucho un despliegue sin terminar por instancia.
    /// </summary>
    public class InstanceDeployment : AuditableEntity<int>, IConcurrencyChecked
    {
        public required int InstanceId { get; set; }
        public required int CustomerId { get; set; }
        public required DeploymentType Type { get; set; }

        /// <summary>Etiqueta de las imágenes que se despliegan (p. ej. 1.4.0).</summary>
        public required string Version { get; set; }

        /// <summary>Solo <c>Create</c>: hasta dónde llegó.</summary>
        public DeploymentStep Step { get; set; }

        /// <summary>
        /// Solo <c>Create</c>, y solo hasta que termina: los secretos del stack (claves, certificado, licencia) generados por la
        /// consola, cifrados con <c>ISecretProtector</c>. Hacen falta para reintentar sin cambiar lo ya cargado; al terminar se
        /// borran (quedan solo en Coolify).
        /// </summary>
        public string? SecretsProtected { get; set; }

        public RemoteActionStatus Status { get; set; } = RemoteActionStatus.Pending;
        public int Attempts { get; set; }
        public DateTime NextAttemptOnUtc { get; set; }
        /// <summary>Mientras está Enviando: hasta cuándo es suyo (si el proceso murió, otro lo retoma pasado ese momento).</summary>
        public DateTime? LockedUntilUtc { get; set; }
        public string? LastError { get; set; }
        public DateTime? DoneOnUtc { get; set; }

        public virtual ErpInstance? Instance { get; set; }
    }
}
