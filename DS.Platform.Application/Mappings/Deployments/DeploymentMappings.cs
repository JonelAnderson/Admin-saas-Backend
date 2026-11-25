using System.Linq.Expressions;
using DS.Platform.Application.DTOs.Deployments.Response;
using DS.Platform.Domain.Entities.Ops;
using DS.Platform.Domain.Enums;

namespace DS.Platform.Application.Mappings.Deployments
{
    /// <summary>Mapeo explícito de la bandeja de despliegues: proyección con la instancia y el cliente por JOIN. Nunca expone los secretos.</summary>
    public static class DeploymentMappings
    {
        public static readonly Expression<Func<InstanceDeployment, InstanceDeploymentResponseDto>> ToResponse = d => new InstanceDeploymentResponseDto
        {
            Id = d.Id,
            InstanceId = d.InstanceId,
            InstanceName = d.Instance != null ? d.Instance.Name : string.Empty,
            InstanceUrl = d.Instance != null ? d.Instance.Url : null,
            CustomerId = d.CustomerId,
            CustomerName = d.Instance != null && d.Instance.Customer != null ? d.Instance.Customer.BusinessName : string.Empty,
            Type = d.Type,
            TypeName = d.Type == DeploymentType.Create ? "Montar instancia" : "Actualizar versión",
            Version = d.Version,
            Step = d.Step,
            StepName = d.Type == DeploymentType.Update ? "-"
                : d.Step == DeploymentStep.None ? "Sin empezar"
                : d.Step == DeploymentStep.ServiceCreated ? "Servicio creado"
                : d.Step == DeploymentStep.Configured ? "Variables cargadas"
                : "Arranque pedido",
            Actor = d.CreatedBy != "" ? d.CreatedBy : "sistema",
            Status = d.Status,
            StatusName = d.Status == RemoteActionStatus.Pending ? "Pendiente"
                : d.Status == RemoteActionStatus.Sending ? "En curso"
                : d.Status == RemoteActionStatus.Done ? "Hecho"
                : d.Status == RemoteActionStatus.Failed ? "Fallido (se reintenta)"
                : d.Status == RemoteActionStatus.Abandoned ? "Abandonado"
                : "Cancelado",
            Attempts = d.Attempts,
            NextAttemptOnUtc = d.NextAttemptOnUtc,
            LastError = d.LastError,
            DoneOnUtc = d.DoneOnUtc,
            CreatedOn = d.CreatedOn
        };
    }
}
