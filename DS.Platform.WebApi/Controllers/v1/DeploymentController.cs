using DS.Platform.Application.Features.Deployments.Commands.Cancel;
using DS.Platform.Application.Features.Deployments.Commands.Provision;
using DS.Platform.Application.Features.Deployments.Commands.Retry;
using DS.Platform.Application.Features.Deployments.Commands.Upgrade;
using DS.Platform.Application.Features.Deployments.Queries.GetAllPaged;
using DS.Platform.Application.Interfaces.Services.Coolify;
using DS.Platform.Domain.Enums;
using DS.Platform.Shared.Commons.Bases.Request;
using DS.Platform.Shared.Constants.Permission;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;

namespace DS.Platform.WebApi.Controllers.v1
{
    /// <summary>Despliegues de instancias del ERP en Coolify (fase C5): montar, actualizar de versión y ver su avance.</summary>
    public class DeploymentController : BaseApiController<DeploymentController>
    {
        [Authorize(Policy = Permissions.Deployments.View)]
        [HttpGet]
        public async Task<IActionResult> Get([FromQuery] BaseFiltersRequest baseFiltersRequest, [FromQuery] RemoteActionStatus? status,
            [FromQuery] int? instanceId, [FromQuery] int? customerId)
            => Ok(await _mediator.Send(new GetAllDeploymentsPagedQuery
            {
                BaseFiltersRequest = baseFiltersRequest, Status = status, InstanceId = instanceId, CustomerId = customerId
            }));

        /// <summary>Datos para el formulario de montaje: dominio base y versión sugerida (nada secreto).</summary>
        [Authorize(Policy = Permissions.Deployments.Menu)]
        [HttpGet("Settings")]
        public IActionResult Settings([FromServices] IProvisioningSettings settings)
            => Ok(new { settings.IsConfigured, settings.BaseDomain, settings.DefaultVersion });

        /// <summary>Monta el stack de una instancia. Devuelve una sola vez la contraseña inicial de los usuarios del ERP.</summary>
        [Authorize(Policy = Permissions.Deployments.Provision)]
        [HttpPost("Provision")]
        public async Task<IActionResult> Provision(ProvisionInstanceCommand command)
            => Ok(await _mediator.Send(command));

        [Authorize(Policy = Permissions.Deployments.Upgrade)]
        [HttpPost("Upgrade")]
        public async Task<IActionResult> Upgrade(UpgradeInstanceCommand command)
            => Ok(await _mediator.Send(command));

        [Authorize(Policy = Permissions.Deployments.Retry)]
        [HttpPut("Retry/{id:int}")]
        public async Task<IActionResult> Retry(int id)
            => Ok(await _mediator.Send(new RetryDeploymentCommand { Id = id }));

        [Authorize(Policy = Permissions.Deployments.Retry)]
        [HttpPut("Cancel/{id:int}")]
        public async Task<IActionResult> Cancel(int id)
            => Ok(await _mediator.Send(new CancelDeploymentCommand { Id = id }));
    }
}
