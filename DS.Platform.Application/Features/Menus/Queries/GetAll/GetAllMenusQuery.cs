using DS.Platform.Application.Mappings.Menus;
using DS.Platform.Application.Interfaces.Repositories;
using DS.Platform.Shared.Static;
using DS.Platform.Shared.Wrapper;
using MediatR;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Localization;
using DS.Platform.Shared.Localization;
using DS.Platform.Application.DTOs.Menu.Response;

namespace DS.Platform.Application.Features.Menus.Queries.GetAll
{
    public class GetAllMenusQuery : IRequest<Result<IEnumerable<MenuResponseDto>>>
    {
    }

    internal class GetAllMenusQueryHandler : IRequestHandler<GetAllMenusQuery, Result<IEnumerable<MenuResponseDto>>>
    {
        private readonly IUnitOfWork<int> _unitOfWork;
        private readonly IStringLocalizer<ServerCommonResources> _localizer;

        public GetAllMenusQueryHandler(IUnitOfWork<int> unitOfWork, IStringLocalizer<ServerCommonResources> localizer)
        {
            _unitOfWork = unitOfWork;
            _localizer = localizer;
        }

        public async Task<Result<IEnumerable<MenuResponseDto>>> Handle(GetAllMenusQuery request, CancellationToken cancellationToken)
        {
            var filteredMenus = await _unitOfWork.Menus.GetAllNotDeletedAsync(cancellationToken);
            var result = filteredMenus.Select(m => m.ToResponseDto()).ToList();

            return await Result<IEnumerable<MenuResponseDto>>.SuccessAsync(
                result,
                _localizer["MessageQuery"],
                filteredMenus.Count);
        }
    }
}
