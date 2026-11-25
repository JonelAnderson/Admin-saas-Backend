using DS.Platform.Application.DTOs.Menu.Response;
using DS.Platform.Application.Interfaces.Repositories;
using DS.Platform.Application.Interfaces.Services;
using DS.Platform.Domain.Entities.Identity;
using DS.Platform.Domain.Entities.Menus;
using DS.Platform.Shared.Constants.Role;
using DS.Platform.Shared.Wrapper;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace DS.Platform.Application.Features.Menus.Queries.GetUserMenu
{
    public class GetUserMenuQueryHandler : IRequestHandler<GetUserMenuQuery, Result<List<MenuNavDto>>>
    {
        private readonly IUnitOfWork<int> _unitOfWork;
        private readonly UserManager<AdmUser> _userManager;
        private readonly RoleManager<AdmRole> _roleManager;
        private readonly ICurrentUserService _currentUserService;

        public GetUserMenuQueryHandler(
            IUnitOfWork<int> unitOfWork,
            UserManager<AdmUser> userManager,
            RoleManager<AdmRole> roleManager,
            ICurrentUserService currentUserService)
        {
            _unitOfWork = unitOfWork;
            _userManager = userManager;
            _roleManager = roleManager;
            _currentUserService = currentUserService;
        }

        public async Task<Result<List<MenuNavDto>>> Handle(GetUserMenuQuery request, CancellationToken cancellationToken)
        {
            var userId = _currentUserService.UserId;
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null) return await Result<List<MenuNavDto>>.FailAsync("User Not Found");

            var roles = await _userManager.GetRolesAsync(user);
            
            // Get all allowed MenuIds for these roles
            var roleIds = new List<string>();
            foreach (var roleName in roles)
            {
                var role = await _roleManager.FindByNameAsync(roleName);
                if (role != null)
                    roleIds.Add(role.Id);
            }

            // The menus of all the roles in one query. SuperAdmin sees every menu, assigned or not.
            var seesEverything = roles.Any(RoleConstants.IsPlatformRole);
            var allowedMenuIds = seesEverything
                ? (await _unitOfWork.Repository<Menu>().GetAllQueryable().Select(m => m.Id).ToListAsync(cancellationToken)).ToHashSet()
                : roleIds.Count == 0
                ? new HashSet<int>()
                : (await _unitOfWork.Repository<MenuRole>()
                    .GetAllQueryable()
                    .Where(mr => roleIds.Contains(mr.RoleId))
                    .Select(mr => mr.MenuId)
                    .ToListAsync(cancellationToken)).ToHashSet();

            // GetAllQueryable already skips deleted menus; inactive ones (Menús → cambiar estado) are hidden too,
            // together with their whole branch (children are never reached).
            var allMenus = await _unitOfWork.Repository<Menu>()
                .GetAllQueryable()
                .Where(m => m.IsActive)
                .OrderBy(m => m.Order)
                .ToListAsync(cancellationToken);

            // Build hierarchical response
            var menuTree = BuildMenuTree(allMenus, allowedMenuIds, null);

            return await Result<List<MenuNavDto>>.SuccessAsync(menuTree);
        }

        private List<MenuNavDto> BuildMenuTree(List<Menu> allMenus, HashSet<int> allowedMenuIds, int? parentId)
        {
            var result = new List<MenuNavDto>();
            var children = allMenus.Where(m => m.MenuParentId == parentId).OrderBy(m => m.Order).ToList();

            foreach (var menu in children)
            {
                var childDtos = BuildMenuTree(allMenus, allowedMenuIds, menu.Id);
                
                // Include this menu if:
                // 1. It is a leaf (item) explicitly allowed
                // 2. OR it has allowed children (so we can navigate to them); parents need no assignment
                // A group/collapse without visible children is skipped even when assigned (empty container).
                bool isContainer = menu.Type is "group" or "collapse";
                bool isAllowed = allowedMenuIds.Contains(menu.Id);
                bool hasAllowedChildren = childDtos.Count > 0;

                if ((isAllowed && !isContainer) || hasAllowedChildren)
                {
                    var dto = new MenuNavDto
                    {
                        Id = menu.Id,
                        Title = menu.Title!,
                        Type = menu.Type,
                        Translate = menu.Translate!,
                        Icon = menu.Icon!,
                        Hidden = menu.Hidden,
                        Url = menu.Url!,
                        Classes = menu.Classes!,
                        ExactMatch = menu.ExactMatch,
                        External = menu.External,
                        Target = menu.Target,
                        Breadcrumbs = menu.Breadcrumbs,
                        BadgeTitle = menu.BadgeTitle!,
                        BadgeType = menu.BadgeType!,
                        Disabled = menu.Disabled,
                        Order = menu.Order,
                        Children = childDtos
                    };
                    result.Add(dto);
                }
            }

            return result;
        }
    }
}
