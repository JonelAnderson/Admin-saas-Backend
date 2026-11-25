using DS.Platform.Domain.Contracts;
using DS.Platform.Domain.Entities.Customers;
using System;

namespace DS.Platform.Domain.Entities.Erp
{
    /// <summary>
    /// Instancia del ERP de un cliente (una por cliente y servidor). Se identifica ante la consola con su clave de licencia, de la
    /// que aquí solo queda el hash (la clave se muestra una vez al crearla o rotarla). Lo que informa la instancia en cada consulta
    /// (versión, uso, último contacto) es telemetría: la escribe la propia instancia y gana el último, por eso no lleva
    /// <c>IConcurrencyChecked</c>.
    /// </summary>
    public class ErpInstance : AuditableEntity<int>
    {
        public required int CustomerId { get; set; }
        /// <summary>Nombre para reconocerla (p. ej. "Producción"); único por cliente.</summary>
        public required string Name { get; set; }
        public string? Url { get; set; }
        public string? Server { get; set; }
        public string? Notes { get; set; }

        /// <summary>Servicio de esta instancia en Coolify (fase C5); vacío si se montó a mano. Lo pone el despachador de despliegues.</summary>
        public string? CoolifyServiceUuid { get; set; }
        /// <summary>Versión de las imágenes que la consola desplegó por última vez (la que <c>Version</c> informa la instancia es telemetría).</summary>
        public string? DeployedVersion { get; set; }

        /// <summary>SHA-256 (hex) de la clave de licencia. Nunca se guarda la clave.</summary>
        public required string LicenseKeyHash { get; set; }
        /// <summary>Últimos 4 caracteres de la clave, para reconocerla en la pantalla.</summary>
        public required string LicenseKeyHint { get; set; }
        public DateTime? KeyRotatedOnUtc { get; set; }

        // Telemetría de la última consulta de la instancia
        public DateTime? LastContactOnUtc { get; set; }
        public string? Version { get; set; }
        public string? LastMigration { get; set; }
        public int? Companies { get; set; }
        public int? Users { get; set; }
        public int? Branches { get; set; }
        public int? DocumentsThisMonth { get; set; }

        public virtual Customer? Customer { get; set; }
    }
}
