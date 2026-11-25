using DS.Platform.Application.DTOs.Menu.Response;
using DS.Platform.Application.Interfaces.Repositories;
using DS.Platform.Shared.Commons.Bases.Request;
using DS.Platform.Shared.Wrapper;
using MediatR;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Localization;
using DS.Platform.Shared.Localization;

namespace DS.Platform.Application.Features.Menus.Queries.GetAllPaged
{
    public class GetAllPagedMenusQuery : IRequest<Result<IEnumerable<MenuResponseDto>>>
    {
        public BaseFiltersRequest BaseFiltersRequest { get; set; } = null!;
    }

    public class GetAllPagedMenusQueryHandler : IRequestHandler<GetAllPagedMenusQuery, Result<IEnumerable<MenuResponseDto>>>
    {
        private readonly IUnitOfWork<int> _unitOfWork;
        private readonly IStringLocalizer<ServerCommonResources> _localizer;

        public GetAllPagedMenusQueryHandler(IUnitOfWork<int> unitOfWork, IStringLocalizer<ServerCommonResources> localizer)
        {
            _unitOfWork = unitOfWork;
            _localizer = localizer;
        }

        public async Task<Result<IEnumerable<MenuResponseDto>>> Handle(GetAllPagedMenusQuery request, CancellationToken cancellationToken)
        {
            var (menus, totalRecords) = await _unitOfWork.Menus.GetAllMenusPaginated(request.BaseFiltersRequest);
            
            return await Result<IEnumerable<MenuResponseDto>>.SuccessAsync(menus, _localizer["MessageQuery"], totalRecords);
        }
    }
}
