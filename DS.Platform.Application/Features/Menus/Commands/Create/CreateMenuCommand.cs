using DS.Platform.Application.Mappings.Menus;
using DS.Platform.Application.Interfaces.Repositories;
using DS.Platform.Application.Interfaces.Services;
using DS.Platform.Domain.Entities.Menus;
using DS.Platform.Shared.Static;
using DS.Platform.Shared.Wrapper;
using MediatR;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Localization;
using DS.Platform.Shared.Localization;

namespace DS.Platform.Application.Features.Menus.Commands.Create
{
    public class CreateMenuCommand : IRequest<Result<int>>
    {
        public string? Title { get; set; }
        public string Type { get; set; } = "item";
        public string? Translate { get; set; }
        public string? Icon { get; set; }
        public bool Hidden { get; set; }
        public string? Url { get; set; }
        public string? Classes { get; set; }
        public bool ExactMatch { get; set; }
        public bool External { get; set; }
        public bool Target { get; set; }
        public bool Breadcrumbs { get; set; }
        public string? BadgeTitle { get; set; }
        public string? BadgeType { get; set; }
        public bool Disabled { get; set; }
        //public List<string>? Permissions { get; set; }
        public int Order { get; set; }
        public int? MenuParentId { get; set; }
    }

    internal class CreateMenuCommandHandler : IRequestHandler<CreateMenuCommand, Result<int>>
    {
        private readonly IUnitOfWork<int> _unitOfWork;
        private readonly ICurrentUserService _currentUser;
        private readonly IStringLocalizer<ServerCommonResources> _localizer;

        public CreateMenuCommandHandler(IUnitOfWork<int> unitOfWork, ICurrentUserService currentUser, IStringLocalizer<ServerCommonResources> localizer)
        {
            _unitOfWork = unitOfWork;
            _currentUser = currentUser;
            _localizer = localizer;
        }

        public async Task<Result<int>> Handle(CreateMenuCommand request, CancellationToken cancellationToken)
        {
            // Los menús son de toda la plataforma: con varias empresas activas solo el SuperAdmin los cambia.
           
            var menu = request.ToEntity();
            await _unitOfWork.Menus.AddAsync(menu);
            await _unitOfWork.Commit(cancellationToken);
                
            return await Result<int>.SuccessAsync(menu.Id, _localizer["MessageSave"]);
           
        }
    }
}
