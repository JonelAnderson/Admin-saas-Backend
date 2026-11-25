using DS.Platform.Application.Features.Payments.Commands.Register;
using DS.Platform.Application.Features.Payments.Commands.Void;
using DS.Platform.Application.Features.Payments.Queries.GetAllPaged;
using DS.Platform.Application.Features.Payments.Queries.GetById;
using DS.Platform.Domain.Enums;
using DS.Platform.Shared.Commons.Bases.Request;
using DS.Platform.Shared.Constants.Permission;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;

namespace DS.Platform.WebApi.Controllers.v1
{
    /// <summary>Pagos de los clientes: se registran (extienden la suscripción) y se anulan con motivo; nunca se editan.</summary>
    public class PaymentController : BaseApiController<PaymentController>
    {
        [Authorize(Policy = Permissions.Payments.View)]
        [HttpGet]
        public async Task<IActionResult> Get([FromQuery] BaseFiltersRequest baseFiltersRequest, [FromQuery] int? customerId,
            [FromQuery] int? subscriptionId, [FromQuery] PaymentStatus? status, [FromQuery] PaymentMethod? method)
            => Ok(await _mediator.Send(new GetAllPaymentsPagedQuery
            {
                BaseFiltersRequest = baseFiltersRequest, CustomerId = customerId, SubscriptionId = subscriptionId, Status = status, Method = method
            }));

        [Authorize(Policy = Permissions.Payments.View)]
        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetById(int id)
            => Ok(await _mediator.Send(new GetPaymentByIdQuery { Id = id }));

        [Authorize(Policy = Permissions.Payments.Create)]
        [HttpPost("Register")]
        public async Task<IActionResult> Register(RegisterPaymentCommand command)
            => Ok(await _mediator.Send(command));

        [Authorize(Policy = Permissions.Payments.Void)]
        [HttpPut("Void")]
        public async Task<IActionResult> Void(VoidPaymentCommand command)
            => Ok(await _mediator.Send(command));
    }
}
