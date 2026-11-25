using DS.Platform.Application.Features.Customers.Commands.ChangeState;
using DS.Platform.Application.Features.Customers.Commands.Create;
using DS.Platform.Application.Features.Customers.Commands.Delete;
using DS.Platform.Application.Features.Customers.Commands.Update;
using DS.Platform.Application.Features.Customers.Queries.GetAllPaged;
using DS.Platform.Application.Features.Customers.Queries.GetById;
using DS.Platform.Application.Features.Customers.Queries.Lookup;
using DS.Platform.Shared.Commons.Bases.Request;
using DS.Platform.Shared.Constants.Permission;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;

namespace DS.Platform.WebApi.Controllers.v1
{
    /// <summary>Clientes de la plataforma (quien contrata el ERP y / o la API SUNAT).</summary>
    public class CustomerController : BaseApiController<CustomerController>
    {
        [Authorize(Policy = Permissions.Customers.View)]
        [HttpGet]
        public async Task<IActionResult> Get([FromQuery] BaseFiltersRequest baseFiltersRequest)
            => Ok(await _mediator.Send(new GetAllCustomersPagedQuery { BaseFiltersRequest = baseFiltersRequest }));

        /// <summary>Buscador por RUC o nombre (máx. 30): combos de suscripciones y pagos.</summary>
        [Authorize(Policy = Permissions.Customers.View)]
        [HttpGet("Lookup")]
        public async Task<IActionResult> Lookup([FromQuery] string? text)
            => Ok(await _mediator.Send(new LookupCustomersQuery { Text = text }));

        [Authorize(Policy = Permissions.Customers.View)]
        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetById(int id)
            => Ok(await _mediator.Send(new GetCustomerByIdQuery { Id = id }));

        [Authorize(Policy = Permissions.Customers.Create)]
        [HttpPost("Create")]
        public async Task<IActionResult> Create(CreateCustomerCommand command)
            => Ok(await _mediator.Send(command));

        [Authorize(Policy = Permissions.Customers.Edit)]
        [HttpPut("Update")]
        public async Task<IActionResult> Update(UpdateCustomerCommand command)
            => Ok(await _mediator.Send(command));

        [Authorize(Policy = Permissions.Customers.ChangeState)]
        [HttpPut("ChangeState/{id:int}")]
        public async Task<IActionResult> ChangeState(int id)
            => Ok(await _mediator.Send(new ChangeStateCustomerCommand { Id = id }));

        [Authorize(Policy = Permissions.Customers.Delete)]
        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id)
            => Ok(await _mediator.Send(new DeleteCustomerCommand { Id = id }));
    }
}
