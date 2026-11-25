using DS.Platform.Application.Interfaces.Repositories;
using DS.Platform.Application.Interfaces.Services;
using DS.Platform.Shared.Static;
using DS.Platform.Shared.Wrapper;
using MediatR;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Localization;
using DS.Platform.Shared.Localization;

namespace DS.Platform.Application.Features.Menus.Commands.Delete
{
    public class DeleteMenuCommand : IRequest<Result<int>>
    {
        public int Id { get; set; }
    }

    internal class DeleteMenuCommandHandler : IRequestHandler<DeleteMenuCommand, Result<int>>
    {
        private readonly IUnitOfWork<int> _unitOfWork;
        private readonly ICurrentUserService _currentUser;
        private readonly IStringLocalizer<ServerCommonResources> _localizer;

        public DeleteMenuCommandHandler(IUnitOfWork<int> unitOfWork, ICurrentUserService currentUser, IStringLocalizer<ServerCommonResources> localizer)
        {
            _unitOfWork = unitOfWork;
            _currentUser = currentUser;
            _localizer = localizer;
        }

        public async Task<Result<int>> Handle(DeleteMenuCommand request, CancellationToken cancellationToken)
        {
            // Los menús son de toda la plataforma: con varias empresas activas solo el SuperAdmin los cambia.
            
            var menu = await _unitOfWork.Menus.GetByIdAsync(request.Id);
            if (menu == null)
            {
                return await Result<int>.FailAsync(_localizer["MessageQueryEmpty"]);
            }

            await _unitOfWork.Menus.DeleteAsync(menu);
            await _unitOfWork.Commit(cancellationToken);
                
            return await Result<int>.SuccessAsync(menu.Id, _localizer["MessageDelete"]);
           
        }
    }
}
