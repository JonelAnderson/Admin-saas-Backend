using DS.Platform.Application.Features.Plans.Commands.ChangeState;
using DS.Platform.Application.Features.Plans.Commands.Create;
using DS.Platform.Application.Features.Plans.Commands.Delete;
using DS.Platform.Application.Features.Plans.Commands.Update;
using DS.Platform.Application.Features.Plans.Queries.GetAllPaged;
using DS.Platform.Application.Features.Plans.Queries.GetById;
using DS.Platform.Application.Features.Plans.Queries.GetSelect;
using DS.Platform.Domain.Enums;
using DS.Platform.Shared.Commons.Bases.Request;
using DS.Platform.Shared.Constants.Permission;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;

namespace DS.Platform.WebApi.Controllers.v1
{
    /// <summary>Planes comerciales del ERP y de la API SUNAT (módulo de referencia de la consola).</summary>
    public class PlanController : BaseApiController<PlanController>
    {
        [Authorize(Policy = Permissions.Plans.View)]
        [HttpGet]
        public async Task<IActionResult> Get([FromQuery] BaseFiltersRequest baseFiltersRequest, [FromQuery] PlanProduct? product)
        {
            var response = await _mediator.Send(new GetAllPlansPagedQuery { BaseFiltersRequest = baseFiltersRequest, Product = product });
            return Ok(response);
        }

        [Authorize(Policy = Permissions.Plans.View)]
        [HttpGet("GetSelect")]
        public async Task<IActionResult> GetSelect([FromQuery] PlanProduct? product)
        {
            var response = await _mediator.Send(new GetPlanSelectQuery { Product = product });
            return Ok(response);
        }

        [Authorize(Policy = Permissions.Plans.View)]
        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetById(int id)
        {
            var response = await _mediator.Send(new GetPlanByIdQuery { Id = id });
            return Ok(response);
        }

        [Authorize(Policy = Permissions.Plans.Create)]
        [HttpPost("Create")]
        public async Task<IActionResult> Create(CreatePlanCommand command)
        {
            var response = await _mediator.Send(command);
            return Ok(response);
        }

        [Authorize(Policy = Permissions.Plans.Edit)]
        [HttpPut("Update")]
        public async Task<IActionResult> Update(UpdatePlanCommand command)
        {
            var response = await _mediator.Send(command);
            return Ok(response);
        }

        [Authorize(Policy = Permissions.Plans.ChangeState)]
        [HttpPut("ChangeState/{id:int}")]
        public async Task<IActionResult> ChangeState(int id)
        {
            var response = await _mediator.Send(new ChangeStatePlanCommand { Id = id });
            return Ok(response);
        }

        [Authorize(Policy = Permissions.Plans.Delete)]
        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id)
        {
            var response = await _mediator.Send(new DeletePlanCommand { Id = id });
            return Ok(response);
        }
    }
}
