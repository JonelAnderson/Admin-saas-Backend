using DS.Platform.Application.Features.Instances.Commands.Create;
using DS.Platform.Application.Features.Instances.Commands.Delete;
using DS.Platform.Application.Features.Instances.Commands.RotateKey;
using DS.Platform.Application.Features.Instances.Commands.Update;
using DS.Platform.Application.Features.Instances.Queries.GetAllPaged;
using DS.Platform.Shared.Commons.Bases.Request;
using DS.Platform.Shared.Constants.Permission;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;

namespace DS.Platform.WebApi.Controllers.v1
{
    /// <summary>Instancias del ERP de los clientes: su clave de licencia, su estado de licencia y su último contacto.</summary>
    public class InstanceController : BaseApiController<InstanceController>
    {
        [Authorize(Policy = Permissions.Instances.View)]
        [HttpGet]
        public async Task<IActionResult> Get([FromQuery] BaseFiltersRequest baseFiltersRequest, [FromQuery] int? customerId)
            => Ok(await _mediator.Send(new GetAllInstancesPagedQuery { BaseFiltersRequest = baseFiltersRequest, CustomerId = customerId }));

        /// <summary>Crea la instancia y devuelve su clave de licencia (una sola vez).</summary>
        [Authorize(Policy = Permissions.Instances.Create)]
        [HttpPost("Create")]
        public async Task<IActionResult> Create(CreateInstanceCommand command)
            => Ok(await _mediator.Send(command));

        [Authorize(Policy = Permissions.Instances.Edit)]
        [HttpPut("Update")]
        public async Task<IActionResult> Update(UpdateInstanceCommand command)
            => Ok(await _mediator.Send(command));

        /// <summary>Genera una clave nueva (la anterior deja de valer) y la devuelve una sola vez.</summary>
        [Authorize(Policy = Permissions.Instances.RotateKey)]
        [HttpPut("RotateKey/{id:int}")]
        public async Task<IActionResult> RotateKey(int id)
            => Ok(await _mediator.Send(new RotateInstanceKeyCommand { Id = id }));

        [Authorize(Policy = Permissions.Instances.Delete)]
        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id)
            => Ok(await _mediator.Send(new DeleteInstanceCommand { Id = id }));
    }
}
