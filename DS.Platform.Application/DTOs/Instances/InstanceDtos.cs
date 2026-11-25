namespace DS.Platform.Application.DTOs.Instances.Request
{
    public class CreateInstanceRequestDto
    {
        public required int CustomerId { get; set; }
        public required string Name { get; set; }
        public string? Url { get; set; }
        public string? Server { get; set; }
        public string? Notes { get; set; }
    }

    public class UpdateInstanceRequestDto
    {
        public required int Id { get; set; }
        public required string Name { get; set; }
        public string? Url { get; set; }
        public string? Server { get; set; }
        public string? Notes { get; set; }
    }

    /// <summary>Lo que informa una instancia del ERP en cada consulta de licencia (nunca datos personales).</summary>
    public class LicenseCheckRequestDto
    {
        /// <summary>Valor al azar de la instancia; la respuesta firmada lo repite para que no se pueda reutilizar una anterior.</summary>
        public string? Nonce { get; set; }
        public string? Version { get; set; }
        public string? LastMigration { get; set; }
        public int? Companies { get; set; }
        public int? Users { get; set; }
        public int? Branches { get; set; }
        public int? DocumentsThisMonth { get; set; }
    }
}

namespace DS.Platform.Application.DTOs.Instances.Response
{
    /// <summary>Instancia del ERP en la lista, con su estado de licencia y de contacto.</summary>
    public class InstanceResponseDto
    {
        public int Id { get; set; }
        public int CustomerId { get; set; }
        public string CustomerRuc { get; set; } = null!;
        public string CustomerName { get; set; } = null!;
        public string Name { get; set; } = null!;
        public string? Url { get; set; }
        public string? Server { get; set; }
        public string? Notes { get; set; }
        public string LicenseKeyHint { get; set; } = null!;
        public DateTime? KeyRotatedOnUtc { get; set; }
        public DateTime? LastContactOnUtc { get; set; }
        public string? Version { get; set; }
        public string? LastMigration { get; set; }
        public int? Companies { get; set; }
        public int? Users { get; set; }
        public int? Branches { get; set; }
        public int? DocumentsThisMonth { get; set; }
        public bool IsActive { get; set; }

        /// <summary>Tiene su stack en Coolify creado por la consola (fase C5).</summary>
        public bool IsProvisioned { get; set; }
        /// <summary>Versión de las imágenes que la consola desplegó por última vez.</summary>
        public string? DeployedVersion { get; set; }

        /// <summary>Estado de la licencia que recibiría hoy (de la suscripción del ERP del cliente): 1 Activa … 4 Cancelada.</summary>
        public int LicenseStatus { get; set; }
        public string LicenseStatusName { get; set; } = null!;
        /// <summary>Contacto: En línea, Con demora, Sin contacto o Nunca se conectó.</summary>
        public string ContactName { get; set; } = null!;
        public string ContactLevel { get; set; } = null!;
    }

    /// <summary>La clave de licencia se muestra una sola vez (al crear o rotar): después solo queda su hash.</summary>
    public class InstanceKeyResponseDto
    {
        public int Id { get; set; }
        public string LicenseKey { get; set; } = null!;
    }

    /// <summary>Respuesta firmada de la licencia: <see cref="Payload"/> es el JSON exacto que se firmó.</summary>
    public class LicenseEnvelopeDto
    {
        public string Payload { get; set; } = null!;
        public string Signature { get; set; } = null!;
        public string Algorithm { get; set; } = "Ed25519";
    }
}
