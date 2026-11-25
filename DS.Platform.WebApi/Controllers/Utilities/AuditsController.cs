using DS.Platform.Application.DTOs.Audits.Request;
using DS.Platform.Application.Features.Audits.Queries.GetAuditTableNames;
using DS.Platform.Application.Features.Audits.Queries.GetAuditTrails;
using DS.Platform.Application.Features.Audits.Queries.GetCurrentUserTrails;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using DS.Platform.Shared.Constants.Permission;

namespace DS.Platform.WebApi.Controllers.Utilities
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class AuditsController : ControllerBase
    {
        private readonly IMediator _mediator;

        public AuditsController(IMediator mediator)
        {
            _mediator = mediator;
        }

        /// <summary>
        /// Get Current User Audit Trails
        /// </summary>
        /// <returns>Status 200 OK</returns>
        [Authorize(Policy = Permissions.AuditTrails.View)]
        [HttpGet]
        public async Task<IActionResult> GetUserTrailsAsync()
        {
            var response = await _mediator.Send(new GetCurrentUserAuditTrailsQuery());
            return Ok(response);
        }

        /// <summary>Pista de auditoría completa, paginada y filtrada (solo SuperAdmin).</summary>
        [Authorize(Policy = Permissions.AuditTrails.View)]
        [HttpGet("Trails")]
        public async Task<IActionResult> GetTrails([FromQuery] AuditTrailFiltersRequest filters)
            => Ok(await _mediator.Send(new GetAuditTrailsQuery { Filters = filters }));

        /// <summary>Entidades que aparecen en la pista (filtro de la pantalla; solo SuperAdmin).</summary>
        [Authorize(Policy = Permissions.AuditTrails.View)]
        [HttpGet("Tables")]
        public async Task<IActionResult> GetTables()
            => Ok(await _mediator.Send(new GetAuditTableNamesQuery()));
    }
}
