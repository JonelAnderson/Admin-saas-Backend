using DS.Platform.Application.Features.Dashboard.Queries.Get;
using DS.Platform.Shared.Constants.Permission;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;

namespace DS.Platform.WebApi.Controllers.v1
{
    /// <summary>Tablero de inicio: clientes por estado, cobrado del mes, vencimientos, instancias y resumen de la API SUNAT.</summary>
    public class DashboardController : BaseApiController<DashboardController>
    {
        [Authorize(Policy = Permissions.Dashboards.View)]
        [HttpGet]
        public async Task<IActionResult> Get() => Ok(await _mediator.Send(new GetDashboardQuery()));
    }
}
