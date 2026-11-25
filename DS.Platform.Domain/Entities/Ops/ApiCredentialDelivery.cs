using DS.Platform.Domain.Contracts;
using DS.Platform.Domain.Entities.ApiSunat;
using DS.Platform.Domain.Entities.Customers;
using DS.Platform.Domain.Entities.Erp;
using DS.Platform.Domain.Enums;
using System;

namespace DS.Platform.Domain.Entities.Ops
{
    /// <summary>
    /// Entrega de las credenciales de la API SUNAT de un emisor a la instancia del ERP de su cliente (fase C6b, contrato en
    /// <c>Docs/erp-api-sunat-sincronizacion-diseno.md</c> §2). Igual que <see cref="InstanceDeployment"/>, la llamada a la API nunca va
    /// dentro del caso de uso: la fila lleva su trabajo pendiente con reintentos y el job <c>ApiCredentialsDispatch</c> lo ejecuta
    /// (regenera las credenciales del emisor y las guarda cifradas). El ERP las recoge con su clave de licencia y las confirma; al
    /// confirmar, al vencer (7 días) o al cancelar el secreto se borra. Como mucho una entrega abierta por emisor.
    /// </summary>
    public class ApiCredentialDelivery : AuditableEntity<int>, IConcurrencyChecked
    {
        public required int InstanceId { get; set; }
        public required int CustomerId { get; set; }
        public required int IssuerId { get; set; }
        /// <summary>Id de la empresa en la API SUNAT (<c>tenants.id</c>); copia del emisor al pedir la entrega.</summary>
        public required long TenantId { get; set; }
        /// <summary>RUC del emisor (11 dígitos): con él el ERP encuentra su empresa.</summary>
        public required string Ruc { get; set; }

        public ApiCredentialDeliveryStatus Status { get; set; } = ApiCredentialDeliveryStatus.Pending;
        public int Attempts { get; set; }
        public DateTime NextAttemptOnUtc { get; set; }
        /// <summary>Mientras está En curso: hasta cuándo es suya.</summary>
        public DateTime? LockedUntilUtc { get; set; }
        public string? LastError { get; set; }

        /// <summary>Cuándo la API generó las credenciales (pasó a Lista).</summary>
        public DateTime? ReadyOnUtc { get; set; }
        /// <summary>Hasta cuándo las espera la consola (7 días desde que quedó Lista); pasado ese momento se borran.</summary>
        public DateTime? ExpiresOnUtc { get; set; }
        /// <summary>Última vez que el ERP preguntó y vio esta entrega (para el "sin recoger" de la pantalla).</summary>
        public DateTime? LastPulledOnUtc { get; set; }
        public DateTime? DeliveredOnUtc { get; set; }

        /// <summary>API key cifrada con <c>ISecretProtector</c>; solo mientras está Lista. Nunca sale de la consola salvo hacia el ERP dueño de la entrega.</summary>
        public string? ApiKeyProtected { get; set; }
        /// <summary>API secret cifrado con <c>ISecretProtector</c>; solo mientras está Lista.</summary>
        public string? ApiSecretProtected { get; set; }

        public virtual ErpInstance? Instance { get; set; }
        public virtual Customer? Customer { get; set; }
        public virtual ApiSunatIssuer? Issuer { get; set; }
    }
}
