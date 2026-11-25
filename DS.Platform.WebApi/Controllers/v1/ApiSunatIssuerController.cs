using DS.Platform.Application.Features.ApiSunat.Commands.Link;
using DS.Platform.Application.Features.ApiSunat.Commands.Sync;
using DS.Platform.Application.Features.ApiSunat.Commands.Unlink;
using DS.Platform.Application.Features.ApiSunat.Queries.GetAllPaged;
using DS.Platform.Application.Features.ApiSunat.Queries.SearchTenants;
using DS.Platform.Shared.Commons.Bases.Request;
using DS.Platform.Shared.Constants.Permission;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;

namespace DS.Platform.WebApi.Controllers.v1
{
    /// <summary>Empresas emisoras de la API SUNAT vinculadas a los clientes de la consola.</summary>
    public class ApiSunatIssuerController : BaseApiController<ApiSunatIssuerController>
    {
        [Authorize(Policy = Permissions.ApiSunatIssuers.View)]
        [HttpGet]
        public async Task<IActionResult> Get([FromQuery] BaseFiltersRequest baseFiltersRequest, [FromQuery] int? customerId, [FromQuery] bool? suspended)
            => Ok(await _mediator.Send(new GetAllIssuersPagedQuery { BaseFiltersRequest = baseFiltersRequest, CustomerId = customerId, Suspended = suspended }));

        /// <summary>Busca empresas en la API SUNAT para vincularlas (máx. 30).</summary>
        [Authorize(Policy = Permissions.ApiSunatIssuers.Link)]
        [HttpGet("Tenants")]
        public async Task<IActionResult> SearchTenants([FromQuery] string? text)
            => Ok(await _mediator.Send(new SearchApiTenantsQuery { Text = text }));

        [Authorize(Policy = Permissions.ApiSunatIssuers.Link)]
        [HttpPost("Link")]
        public async Task<IActionResult> Link(LinkIssuerCommand command)
            => Ok(await _mediator.Send(command));

        [Authorize(Policy = Permissions.ApiSunatIssuers.Sync)]
        [HttpPut("Sync/{id:int}")]
        public async Task<IActionResult> Sync(int id)
            => Ok(await _mediator.Send(new SyncIssuerCommand { Id = id }));

        [Authorize(Policy = Permissions.ApiSunatIssuers.Unlink)]
        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Unlink(int id)
            => Ok(await _mediator.Send(new UnlinkIssuerCommand { Id = id }));
    }
}
