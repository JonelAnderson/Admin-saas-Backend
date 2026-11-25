using DS.Platform.Application.DTOs.Menu.Response;
using DS.Platform.Application.DTOs.Menu;
using DS.Platform.Shared.Wrapper;
using MediatR;
using System.Collections.Generic;

namespace DS.Platform.Application.Features.Menus.Queries.GetUserMenu
{
    public class GetUserMenuQuery : IRequest<Result<List<MenuNavDto>>>
    {
        public string UserId { get; set; } = null!;
    }
}
