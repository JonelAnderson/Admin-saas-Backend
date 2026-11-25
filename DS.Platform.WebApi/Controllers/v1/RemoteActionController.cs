using DS.Platform.Application.Features.RemoteActions.Commands.Retry;
using DS.Platform.Application.Features.RemoteActions.Queries.GetAllPaged;
using DS.Platform.Domain.Enums;
using DS.Platform.Shared.Commons.Bases.Request;
using DS.Platform.Shared.Constants.Permission;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;

namespace DS.Platform.WebApi.Controllers.v1
{
    /// <summary>Bandeja de acciones remotas: lo que la consola pidió a la API SUNAT y su resultado.</summary>
    public class RemoteActionController : BaseApiController<RemoteActionController>
    {
        [Authorize(Policy = Permissions.RemoteActions.View)]
        [HttpGet]
        public async Task<IActionResult> Get([FromQuery] BaseFiltersRequest baseFiltersRequest, [FromQuery] RemoteActionStatus? status,
            [FromQuery] int? issuerId, [FromQuery] int? customerId)
            => Ok(await _mediator.Send(new GetAllRemoteActionsPagedQuery
            {
                BaseFiltersRequest = baseFiltersRequest, Status = status, IssuerId = issuerId, CustomerId = customerId
            }));

        [Authorize(Policy = Permissions.RemoteActions.Retry)]
        [HttpPut("Retry/{id:int}")]
        public async Task<IActionResult> Retry(int id)
            => Ok(await _mediator.Send(new RetryRemoteActionCommand { Id = id }));
    }
}
