using DS.Platform.Application.DTOs.Portal.Request;
using DS.Platform.Application.Features.Portal.Commands.Invite;
using DS.Platform.Application.Features.Portal.Commands.Retry;
using DS.Platform.Application.Features.Portal.Commands.SetEnabled;
using DS.Platform.Application.Features.Portal.Queries.GetAllPaged;
using DS.Platform.Application.Interfaces.Services.Portal;
using DS.Platform.Domain.Enums;
using DS.Platform.Shared.Commons.Bases.Request;
using DS.Platform.Shared.Constants.Permission;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;

namespace DS.Platform.WebApi.Controllers.v1
{
    /// <summary>Acceso de personas de los clientes al portal de la API SUNAT (fase C6): invitar, reinvitar, habilitar y deshabilitar.</summary>
    public class PortalAccessController : BaseApiController<PortalAccessController>
    {
        [Authorize(Policy = Permissions.PortalAccess.View)]
        [HttpGet]
        public async Task<IActionResult> Get([FromQuery] BaseFiltersRequest baseFiltersRequest, [FromQuery] RemoteActionStatus? status,
            [FromQuery] int? customerId, [FromQuery] bool? isEnabled)
            => Ok(await _mediator.Send(new GetAllPortalAccessesPagedQuery
            {
                BaseFiltersRequest = baseFiltersRequest, Status = status, CustomerId = customerId, IsEnabled = isEnabled
            }));

        /// <summary>Si el portal tiene dirección pública (sin ella las invitaciones esperan). Nada secreto.</summary>
        [Authorize(Policy = Permissions.PortalAccess.Menu)]
        [HttpGet("Settings")]
        public IActionResult Settings([FromServices] IPortalSettings settings)
            => Ok(new { settings.IsConfigured, settings.PublicUrl });

        /// <summary>Invita o reinvita a una persona (el correo identifica el acceso). El enlace de activación llega por correo.</summary>
        [Authorize(Policy = Permissions.PortalAccess.Invite)]
        [HttpPost("Invite")]
        public async Task<IActionResult> Invite(InviteToPortalRequestDto dto)
            => Ok(await _mediator.Send(new InviteToPortalCommand { InviteToPortalRequestDto = dto }));

        [Authorize(Policy = Permissions.PortalAccess.Manage)]
        [HttpPut("{id:int}/Enabled/{enabled:bool}")]
        public async Task<IActionResult> SetEnabled(int id, bool enabled)
            => Ok(await _mediator.Send(new SetPortalAccessEnabledCommand { Id = id, Enabled = enabled }));

        [Authorize(Policy = Permissions.PortalAccess.Manage)]
        [HttpPut("Retry/{id:int}")]
        public async Task<IActionResult> Retry(int id)
            => Ok(await _mediator.Send(new RetryPortalAccessCommand { Id = id }));
    }
}
