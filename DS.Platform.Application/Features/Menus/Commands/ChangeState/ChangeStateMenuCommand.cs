using DS.Platform.Application.Interfaces.Repositories;
using DS.Platform.Application.Interfaces.Services;
using DS.Platform.Shared.Wrapper;
using MediatR;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Localization;
using DS.Platform.Shared.Localization;

namespace DS.Platform.Application.Features.Menus.Commands.ChangeState
{
    public class ChangeStateMenuCommand : IRequest<Result<int>>
    {
        public int Id { get; set; }
    }

    internal class ChangeStateMenuCommandHandler : IRequestHandler<ChangeStateMenuCommand, Result<int>>
    {
        private readonly IUnitOfWork<int> _unitOfWork;
        private readonly ICurrentUserService _currentUser;
        private readonly IStringLocalizer<ServerCommonResources> _localizer;

        public ChangeStateMenuCommandHandler(IUnitOfWork<int> unitOfWork, ICurrentUserService currentUser, IStringLocalizer<ServerCommonResources> localizer)
        {
            _unitOfWork = unitOfWork;
            _currentUser = currentUser;
            _localizer = localizer;
        }

        public async Task<Result<int>> Handle(ChangeStateMenuCommand request, CancellationToken cancellationToken)
        {
            // Los menús son de toda la plataforma: con varias empresas activas solo el SuperAdmin los cambia.
            var menu = await _unitOfWork.Menus.GetByIdAsync(request.Id);
            if (menu == null)
            {
                return await Result<int>.FailAsync(_localizer["MessageQueryEmpty"]);
            }

            menu.IsActive = !menu.IsActive;
            await _unitOfWork.Menus.UpdateAsync(menu);
            await _unitOfWork.Commit(cancellationToken);

            return await Result<int>.SuccessAsync(menu.Id, _localizer["MessageUpdate"]);
        }
    }
}
