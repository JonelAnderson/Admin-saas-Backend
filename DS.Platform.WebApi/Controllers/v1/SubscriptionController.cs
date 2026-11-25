using DS.Platform.Application.Features.Subscriptions.Commands.Cancel;
using DS.Platform.Application.Features.Subscriptions.Commands.Create;
using DS.Platform.Application.Features.Subscriptions.Commands.Extend;
using DS.Platform.Application.Features.Subscriptions.Commands.Reactivate;
using DS.Platform.Application.Features.Subscriptions.Commands.Suspend;
using DS.Platform.Application.Features.Subscriptions.Commands.Update;
using DS.Platform.Application.Features.Subscriptions.Queries.GetAllPaged;
using DS.Platform.Application.Features.Subscriptions.Queries.GetById;
using DS.Platform.Domain.Enums;
using DS.Platform.Shared.Commons.Bases.Request;
using DS.Platform.Shared.Constants.Permission;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;

namespace DS.Platform.WebApi.Controllers.v1
{
    /// <summary>Suscripciones: qué cliente tiene qué plan, con su estado (prueba, activa, en gracia, suspendida, cancelada).</summary>
    public class SubscriptionController : BaseApiController<SubscriptionController>
    {
        [Authorize(Policy = Permissions.Subscriptions.View)]
        [HttpGet]
        public async Task<IActionResult> Get([FromQuery] BaseFiltersRequest baseFiltersRequest, [FromQuery] int? customerId,
            [FromQuery] int? planId, [FromQuery] PlanProduct? product, [FromQuery] SubscriptionStatus? status)
            => Ok(await _mediator.Send(new GetAllSubscriptionsPagedQuery
            {
                BaseFiltersRequest = baseFiltersRequest, CustomerId = customerId, PlanId = planId, Product = product, Status = status
            }));

        [Authorize(Policy = Permissions.Subscriptions.View)]
        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetById(int id)
            => Ok(await _mediator.Send(new GetSubscriptionByIdQuery { Id = id }));

        [Authorize(Policy = Permissions.Subscriptions.Create)]
        [HttpPost("Create")]
        public async Task<IActionResult> Create(CreateSubscriptionCommand command)
            => Ok(await _mediator.Send(command));

        [Authorize(Policy = Permissions.Subscriptions.Edit)]
        [HttpPut("Update")]
        public async Task<IActionResult> Update(UpdateSubscriptionCommand command)
            => Ok(await _mediator.Send(command));

        [Authorize(Policy = Permissions.Subscriptions.Extend)]
        [HttpPut("Extend")]
        public async Task<IActionResult> Extend(ExtendSubscriptionCommand command)
            => Ok(await _mediator.Send(command));

        [Authorize(Policy = Permissions.Subscriptions.Suspend)]
        [HttpPut("Suspend")]
        public async Task<IActionResult> Suspend(SuspendSubscriptionCommand command)
            => Ok(await _mediator.Send(command));

        [Authorize(Policy = Permissions.Subscriptions.Suspend)]
        [HttpPut("Reactivate")]
        public async Task<IActionResult> Reactivate(ReactivateSubscriptionCommand command)
            => Ok(await _mediator.Send(command));

        [Authorize(Policy = Permissions.Subscriptions.Cancel)]
        [HttpPut("Cancel")]
        public async Task<IActionResult> Cancel(CancelSubscriptionCommand command)
            => Ok(await _mediator.Send(command));
    }
}
