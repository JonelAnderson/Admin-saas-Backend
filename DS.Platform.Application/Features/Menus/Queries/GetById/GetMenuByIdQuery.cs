using DS.Platform.Application.Interfaces.Repositories;
using DS.Platform.Domain.Entities.Menus;
using DS.Platform.Shared.Static;
using DS.Platform.Shared.Wrapper;
using MediatR;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Localization;
using DS.Platform.Shared.Localization;

namespace DS.Platform.Application.Features.Menus.Queries.GetById
{
    public class GetMenuByIdQuery : IRequest<Result<Menu>>
    {
        public int Id { get; set; }
    }

    internal class GetMenuByIdQueryHandler : IRequestHandler<GetMenuByIdQuery, Result<Menu>>
    {
        private readonly IUnitOfWork<int> _unitOfWork;
        private readonly IStringLocalizer<ServerCommonResources> _localizer;

        public GetMenuByIdQueryHandler(IUnitOfWork<int> unitOfWork, IStringLocalizer<ServerCommonResources> localizer)
        {
            _unitOfWork = unitOfWork;
            _localizer = localizer;
        }

        public async Task<Result<Menu>> Handle(GetMenuByIdQuery request, CancellationToken cancellationToken)
        {
           
            var menu = await _unitOfWork.Menus.GetByIdAsync(request.Id);
            if (menu == null)
            {
                return await Result<Menu>.FailAsync(_localizer["MessageQueryEmpty"]);
            }
            return await Result<Menu>.SuccessAsync(menu, _localizer["MessageQuery"]);
            
        }
    }
}
