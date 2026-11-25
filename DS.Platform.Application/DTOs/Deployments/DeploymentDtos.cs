using DS.Platform.Domain.Enums;

namespace DS.Platform.Application.DTOs.Deployments.Request
{
    /// <summary>Monta el stack de una instancia registrada: subdominio (la parte antes del dominio base) y versión de las imágenes.</summary>
    public class ProvisionInstanceRequestDto
    {
        public required int InstanceId { get; set; }
        public required string Subdomain { get; set; }
        public required string Version { get; set; }
    }

    /// <summary>Lleva el stack de una instancia a otra versión de las imágenes.</summary>
    public class UpgradeInstanceRequestDto
    {
        public required int InstanceId { get; set; }
        public required string Version { get; set; }
    }
}

namespace DS.Platform.Application.DTOs.Deployments.Response
{
    /// <summary>
    /// Lo que se entrega **una sola vez** al montar una instancia: la contraseña inicial de los usuarios del ERP del cliente.
    /// La clave de licencia no se muestra: va directo al stack (en la lista solo se ve su terminación).
    /// </summary>
    public class ProvisionInstanceResponseDto
    {
        public int DeploymentId { get; set; }
        public string Url { get; set; } = null!;
        public string InitialAdminUser { get; set; } = null!;
        public string InitialAdminPassword { get; set; } = null!;
    }

    /// <summary>Fila de la bandeja de despliegues.</summary>
    public class InstanceDeploymentResponseDto
    {
        public int Id { get; set; }
        public int InstanceId { get; set; }
        public string InstanceName { get; set; } = null!;
        public string? InstanceUrl { get; set; }
        public int CustomerId { get; set; }
        public string CustomerName { get; set; } = null!;
        public DeploymentType Type { get; set; }
        public string TypeName { get; set; } = null!;
        public string Version { get; set; } = null!;
        public DeploymentStep Step { get; set; }
        public string StepName { get; set; } = null!;
        /// <summary>Usuario de la consola que lo pidió.</summary>
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
