using DS.Platform.Application.Features.Menus.Commands.Create;
using DS.Platform.Application.Features.Menus.Commands.Delete;
using DS.Platform.Application.Features.Menus.Commands.Update;
using DS.Platform.Application.Features.Menus.Queries.GetAllPaged;
using DS.Platform.Application.Features.Menus.Queries.GetById;
using DS.Platform.Application.Features.Menus.Queries.GetUserMenu;
using DS.Platform.Application.Features.Menus.Queries.GetAll;
using DS.Platform.Application.Features.Menus.Commands.ChangeState;
using DS.Platform.Shared.Commons.Bases.Request;
using DS.Platform.Shared.Constants.Permission;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;

namespace DS.Platform.WebApi.Controllers.v1
{
    public class MenuController : BaseApiController<MenuController>
    {
        [Authorize(Policy = Permissions.Menus.Create)]
        [HttpPost("Create")]
        public async Task<IActionResult> Create(CreateMenuCommand command)
        {
            var response = await _mediator.Send(command);
            return Ok(response);
        }

        [Authorize(Policy = Permissions.Menus.Edit)]
        [HttpPut("Update")]
        public async Task<IActionResult> Update(UpdateMenuCommand command)
        {
            var response = await _mediator.Send(command);
            return Ok(response);
        }

        [Authorize(Policy = Permissions.Menus.Delete)]
        [HttpDelete("Delete/{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var response = await _mediator.Send(new DeleteMenuCommand { Id = id });
            return Ok(response);
        }

        [Authorize]
        [HttpGet("Nav")]
        public async Task<IActionResult> GetNav()
        {
            var response = await _mediator.Send(new GetUserMenuQuery());
            return Ok(response);
        }

        [Authorize(Policy = Permissions.Menus.View)]
        [HttpGet("Select")]
        public async Task<IActionResult> GetSelect()
        {
            var response = await _mediator.Send(new GetAllMenusQuery());
            return Ok(response);
        }

        [Authorize(Policy = Permissions.Menus.View)]
        [HttpGet]
        public async Task<IActionResult> Get([FromQuery] BaseFiltersRequest baseFiltersRequest)
        {
            var response = await _mediator.Send(new GetAllPagedMenusQuery() { BaseFiltersRequest = baseFiltersRequest });
            return Ok(response);
        }

        [Authorize(Policy = Permissions.Menus.Edit)]
        [HttpPut("ChangeState/{id:int}")]
        public async Task<IActionResult> ChangeState(int id)
        {
            var response = await _mediator.Send(new ChangeStateMenuCommand { Id = id });
            return Ok(response);
        }

        [Authorize(Policy = Permissions.Menus.View)]
        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetById(int id)
        {
            var response = await _mediator.Send(new GetMenuByIdQuery { Id = id });
            return Ok(response);
        }
    }
}
