using DS.Platform.Application.Features.Notifications.Commands.Retry;
using DS.Platform.Application.Features.Notifications.Queries.GetAllPaged;
using DS.Platform.Domain.Enums;
using DS.Platform.Shared.Commons.Bases.Request;
using DS.Platform.Shared.Constants.Permission;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;

namespace DS.Platform.WebApi.Controllers.v1
{
    /// <summary>Bandeja de avisos por correo a los clientes: lo que se avisó, a quién y su resultado.</summary>
    public class NotificationController : BaseApiController<NotificationController>
    {
        [Authorize(Policy = Permissions.Notifications.View)]
        [HttpGet]
        public async Task<IActionResult> Get([FromQuery] BaseFiltersRequest baseFiltersRequest, [FromQuery] NotificationStatus? status,
            [FromQuery] NotificationType? type, [FromQuery] int? customerId)
            => Ok(await _mediator.Send(new GetAllNotificationsPagedQuery
            {
                BaseFiltersRequest = baseFiltersRequest, Status = status, Type = type, CustomerId = customerId
            }));

        [Authorize(Policy = Permissions.Notifications.Retry)]
        [HttpPut("Retry/{id:int}")]
        public async Task<IActionResult> Retry(int id)
            => Ok(await _mediator.Send(new RetryNotificationCommand { Id = id }));
    }
}
