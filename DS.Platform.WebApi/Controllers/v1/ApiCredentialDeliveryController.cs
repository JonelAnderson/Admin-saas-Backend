using DS.Platform.Application.DTOs.Credentials.Request;
using DS.Platform.Application.Features.Credentials.Commands.Cancel;
using DS.Platform.Application.Features.Credentials.Commands.Create;
using DS.Platform.Application.Features.Credentials.Commands.Retry;
using DS.Platform.Application.Features.Credentials.Queries.GetAllPaged;
using DS.Platform.Application.Interfaces.Services.ApiSunat;
using DS.Platform.Domain.Enums;
using DS.Platform.Shared.Commons.Bases.Request;
using DS.Platform.Shared.Constants.Permission;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;

namespace DS.Platform.WebApi.Controllers.v1
{
    /// <summary>
    /// Entrega de las credenciales de la API SUNAT de un emisor a la instancia del ERP de su cliente (fase C6b): pedir, reintentar y
    /// cancelar. El ERP las recoge solo por <c>License/ApiSunat/Pull</c>; ninguna respuesta de este controlador lleva la key ni el secret.
    /// </summary>
    public class ApiCredentialDeliveryController : BaseApiController<ApiCredentialDeliveryController>
    {
        [Authorize(Policy = Permissions.ApiCredentialDeliveries.View)]
        [HttpGet]
        public async Task<IActionResult> Get([FromQuery] BaseFiltersRequest baseFiltersRequest, [FromQuery] ApiCredentialDeliveryStatus? status,
            [FromQuery] int? customerId, [FromQuery] int? instanceId)
            => Ok(await _mediator.Send(new GetAllApiCredentialDeliveriesPagedQuery
            {
                BaseFiltersRequest = baseFiltersRequest, Status = status, CustomerId = customerId, InstanceId = instanceId
            }));

        /// <summary>Si la API SUNAT tiene dirección pública (sin ella las entregas esperan). Nada secreto.</summary>
        [Authorize(Policy = Permissions.ApiCredentialDeliveries.Menu)]
        [HttpGet("Settings")]
        public IActionResult Settings([FromServices] IApiSunatSettings settings)
            => Ok(new { isConfigured = settings.IsPublicUrlConfigured, publicUrl = settings.PublicUrl });

        /// <summary>Pide la entrega: regenerar las credenciales del emisor invalida las actuales (el cuerpo debe confirmarlo).</summary>
        [Authorize(Policy = Permissions.ApiCredentialDeliveries.Create)]
        [HttpPost]
        public async Task<IActionResult> Post(CreateApiCredentialDeliveryRequestDto dto)
            => Ok(await _mediator.Send(new CreateApiCredentialDeliveryCommand { CreateApiCredentialDeliveryRequestDto = dto }));

        [Authorize(Policy = Permissions.ApiCredentialDeliveries.Retry)]
        [HttpPut("Retry/{id:int}")]
        public async Task<IActionResult> Retry(int id)
            => Ok(await _mediator.Send(new RetryApiCredentialDeliveryCommand { Id = id }));

        [Authorize(Policy = Permissions.ApiCredentialDeliveries.Cancel)]
        [HttpPut("Cancel/{id:int}")]
        public async Task<IActionResult> Cancel(int id)
            => Ok(await _mediator.Send(new CancelApiCredentialDeliveryCommand { Id = id }));
    }
}
