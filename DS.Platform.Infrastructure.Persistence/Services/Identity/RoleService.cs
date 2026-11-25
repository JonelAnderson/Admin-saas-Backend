using DS.Platform.Infrastructure.Mappings;
using DS.Platform.Application.DTOs.Identities.Request;
using DS.Platform.Application.DTOs.Identities.Response;
using DS.Platform.Application.Interfaces.Services;
using DS.Platform.Application.Interfaces.Services.Identity;
using DS.Platform.Application.Interfaces.Repositories;
using DS.Platform.Domain.Entities.Menus;
using DS.Platform.Domain.Entities;
using DS.Platform.Domain.Entities.Identity;
using DS.Platform.Infrastructure.Helpers;
using DS.Platform.Shared.Constants.Role;
using DS.Platform.Shared.Wrapper;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Localization;
using DS.Platform.Shared.Localization;

namespace DS.Platform.Infrastructure.Services.Identity
{
    public class RoleService : IRoleService
    {
        private readonly RoleManager<AdmRole> _roleManager;
        private readonly UserManager<AdmUser> _userManager;
        private readonly IRoleClaimService _roleClaimService;
        private readonly ICurrentUserService _currentUserService;
        private readonly IUnitOfWork<int> _unitOfWork;
        private readonly IStringLocalizer<ServerCommonResources> _localizer;
        private readonly IUserPermissionCache _permissionCache;

        public RoleService(
            RoleManager<AdmRole> roleManager,
            UserManager<AdmUser> userManager,
            IRoleClaimService roleClaimService,
            ICurrentUserService currentUserService,
            IUnitOfWork<int> unitOfWork,
            IStringLocalizer<ServerCommonResources> localizer,
            IUserPermissionCache permissionCache
            )
        {
            _roleManager = roleManager;
            _userManager = userManager;
            _roleClaimService = roleClaimService;
            _permissionCache = permissionCache;
            _currentUserService = currentUserService;
            _unitOfWork = unitOfWork;
            _localizer = localizer;
        }


        public async Task<Result<IEnumerable<RoleResponse>>> GetAllAsync()
        {
            var roles = await _roleManager.Roles.Where(x => x.IsDeleted == false && x.IsActive == true).ToListAsync();
            var rolesResponse = roles.Select(x => x.ToResponse()).ToList();
            return await Result<IEnumerable<RoleResponse>>.SuccessAsync(rolesResponse);
        }

        public async Task<Result<PermissionResponse>> GetAllPermissionsAsync(string roleId)
        {
            var model = new PermissionResponse();
            var allPermissions = GetAllPermissions();
            var role = await _roleManager.FindByIdAsync(roleId);
            if (role != null)
            {
                model.RoleId = role.Id;
                model.RoleName = role.Name;
                var roleClaimsResult = await _roleClaimService.GetAllByRoleIdAsync(role.Id);
                if (roleClaimsResult.Succeeded)
                {
                    var roleClaims = roleClaimsResult.Data;
                    var allClaimValues = allPermissions.Select(a => a.Value).ToList();
                    var roleClaimValues = roleClaims.Select(a => a.Value).ToList();
                    var authorizedClaims = allClaimValues.Intersect(roleClaimValues).ToList();
                    foreach (var permission in allPermissions)
                    {
                        if (authorizedClaims.Any(a => a == permission.Value))
                        {
                            permission.Selected = true;
                            var roleClaim = roleClaims.SingleOrDefault(a => a.Value == permission.Value);
                            if (roleClaim?.Description != null)
                            {
                                permission.Description = roleClaim.Description;
                            }
                            if (roleClaim?.Group != null)
                            {
                                permission.Group = roleClaim.Group;
                            }
                        }
                    }
                }
                else
                {
                    model.RoleClaims = new List<RoleClaimResponse>();
                    return await Result<PermissionResponse>.FailAsync(roleClaimsResult.Messages);
                }
            }
            model.RoleClaims = allPermissions;
            return await Result<PermissionResponse>.SuccessAsync(model);
        }


        public async Task<Result<PermissionResponse>> GetAllPermissionsByRoleNameAsync(string roleName)
        {
            var model = new PermissionResponse();
            var allPermissions = GetAllPermissions();
            var role = await _roleManager.FindByNameAsync(roleName);
            if (role != null)
            {
                model.RoleId = role.Id;
                model.RoleName = role.Name;
                var roleClaimsResult = await _roleClaimService.GetAllByRoleIdAsync(role.Id);
                if (roleClaimsResult.Succeeded)
                {
                    var roleClaims = roleClaimsResult.Data;
                    var allClaimValues = allPermissions.Select(a => a.Value).ToList();
                    var roleClaimValues = roleClaims.Select(a => a.Value).ToList();
                    var authorizedClaims = allClaimValues.Intersect(roleClaimValues).ToList();
                    foreach (var permission in allPermissions)
                    {
                        if (authorizedClaims.Any(a => a == permission.Value))
                        {
                            permission.Selected = true;
                            var roleClaim = roleClaims.SingleOrDefault(a => a.Value == permission.Value);
                            if (roleClaim?.Description != null)
                            {
                                permission.Description = roleClaim.Description;
                            }
                            if (roleClaim?.Group != null)
                            {
                                permission.Group = roleClaim.Group;
                            }
                        }
                    }
                }
                else
                {
                    model.RoleClaims = new List<RoleClaimResponse>();
                    return await Result<PermissionResponse>.FailAsync(roleClaimsResult.Messages);
                }
            }
            model.RoleClaims = allPermissions;
            return await Result<PermissionResponse>.SuccessAsync(model);
        }


        private List<RoleClaimResponse> GetAllPermissions()
        {
            var allPermissions = new List<RoleClaimResponse>();

            #region GetPermissions

            allPermissions.GetAllPermissions();

            #endregion GetPermissions

            return allPermissions;
        }

        public async Task<Result<RoleResponse>> GetByIdAsync(string id)
        {
            var roles = await _roleManager.Roles.SingleOrDefaultAsync(x => x.Id == id);
            var rolesResponse = roles?.ToResponse();
            return await Result<RoleResponse>.SuccessAsync(rolesResponse);
        }

        public async Task<Result<string>> SaveAsync(RoleRequest request)
        {
            // Un rol propio no puede llamarse como uno de plataforma o del sistema: sus miembros heredarían ese acceso
            // (IsPlatformRole busca por nombre) o el seeder lo renombraría.
            if (SystemRoleCatalog.IsReservedName(request.Name))
            {
                return await Result<string>.FailAsync(string.Format(_localizer["RoleNotAllowedModify"], request.Name));
            }
            request.Name = request.Name!.Trim();
            if (string.IsNullOrEmpty(request.Id))
            {
                var existingRole = await _roleManager.FindByNameAsync(request.Name!);
                if (existingRole != null) return await Result<string>.FailAsync(_localizer["RoleExists"]);
                var response = await _roleManager.CreateAsync(new AdmRole(request.Name!, request.Description!));
                if (response.Succeeded)
                {
                    return await Result<string>.SuccessAsync(string.Format(_localizer["RoleCreated"], request.Name));
                }
                else
                {
                    return await Result<string>.FailAsync(response.Errors.Select(e => e.Description.ToString()).ToList());
                }
            }
            else
            {
                var existingRole = await _roleManager.FindByIdAsync(request.Id);
                if (existingRole is null)
                {
                    return await Result<string>.FailAsync(_localizer["MessageQueryEmpty"]);
                }
                // Los roles del sistema (SystemRoleCatalog) no se renombran: el seeder los busca por nombre.
                if (SystemRoleCatalog.IsSystemRole(existingRole.Name) || RoleConstants.IsPlatformRole(existingRole.Name))
                {
                    return await Result<string>.FailAsync(string.Format(_localizer["RoleNotAllowedModify"], existingRole.Name));
                }
                existingRole.Name = request.Name;
                existingRole.NormalizedName = _roleManager.NormalizeKey(request.Name);
                existingRole.Description = request.Description!;
                // UpdateAsync valida (p. ej. nombre repetido): si falla, no se informa éxito.
                var updated = await _roleManager.UpdateAsync(existingRole);
                if (!updated.Succeeded)
                {
                    return await Result<string>.FailAsync(updated.Errors.Select(e => e.Description).ToList());
                }
                return await Result<string>.SuccessAsync(string.Format(_localizer["RoleUpdated"], existingRole.Name));
            }
        }

        public async Task<Result<string>> UpdatePermissionsAsync(PermissionRequest request)
        {
            try
            {
                var errors = new List<string>();
                var role = await _roleManager.FindByIdAsync(request!.RoleId!);
                if (role is null)
                {
                    return await Result<string>.FailAsync(_localizer["MessageQueryEmpty"]);
                }
                // SuperAdmin tiene siempre todos los permisos (el seeder los sincroniza y la autorización no los mira).
                if (RoleConstants.IsPlatformRole(role.Name))
                {
                    return await Result<string>.FailAsync(string.Format(_localizer["RoleNotAllowedModify"], role.Name));
                }
                //if (role!.Name == RoleConstants.AdministratorRole)
                //{
                //    var currentUser = await _userManager.Users.SingleAsync(x => x.Id == _currentUserService.UserId);
                //    if (await _userManager.IsInRoleAsync(currentUser, RoleConstants.AdministratorRole))
                //    {
                //        return await Result<string>.FailAsync("Not allowed to modify Permissions for this Role.");
                //    }
                //}

                var selectedClaims = request.RoleClaims!.Where(a => a.Selected).ToList();
                //if (role.Name == RoleConstants.AdministratorRole)
                //{
                //    if (!selectedClaims.Any(x => x.Value == Permissions.Roles.View)
                //       || !selectedClaims.Any(x => x.Value == Permissions.RoleClaims.View)
                //       || !selectedClaims.Any(x => x.Value == Permissions.RoleClaims.Edit))
                //    {
                //        return await Result<string>.FailAsync(string.Format(
                //            "Not allowed to deselect {0} or {1} or {2} for this Role.",
                //            Permissions.Roles.View, Permissions.RoleClaims.View, Permissions.RoleClaims.Edit));
                //    }
                //}

                var claims = await _roleManager.GetClaimsAsync(role!);
                foreach (var claim in claims)
                {
                    await _roleManager.RemoveClaimAsync(role!, claim);
                }
                foreach (var claim in selectedClaims)
                {
                    var addResult = await _roleManager.AddPermissionClaim(role!, claim.Value!);
                    if (!addResult.Succeeded)
                    {
                        errors.AddRange(addResult.Errors.Select(e => e.Description.ToString()));
                    }
                }

                var addedClaims = await _roleClaimService.GetAllByRoleIdAsync(role!.Id);
                if (addedClaims.Succeeded)
                {
                    foreach (var claim in selectedClaims)
                    {
                        var addedClaim = addedClaims.Data.SingleOrDefault(x => x.Type == claim.Type && x.Value == claim.Value);
                        if (addedClaim != null)
                        {
                            claim.Id = addedClaim.Id;
                            claim.RoleId = addedClaim.RoleId;
                            var saveResult = await _roleClaimService.SaveAsync(claim);
                            if (!saveResult.Succeeded)
                            {
                                errors.AddRange(saveResult.Messages);
                            }
                        }
                    }
                }
                else
                {
                    errors.AddRange(addedClaims.Messages);
                }

                // Los permisos del rol cambiaron (aunque sea en parte): sus usuarios dejan de usar los de la caché.
                _permissionCache.InvalidateAll();

                if (errors.Any())
                {
                    return await Result<string>.FailAsync(errors);
                }

                return await Result<string>.SuccessAsync(_localizer["PermissionsUpdated"]);
            }
            catch (Exception ex)
            {
                return await Result<string>.FailAsync(ex.Message);
            }
        }

        public async Task<int> GetCountAsync()
        {
            var count = await _roleManager.Roles.CountAsync();
            return count;
        }

        public async Task<Result<string>> DeleteAsync(string id)
        {
            var existingRole = await _roleManager.FindByIdAsync(id);
            if (!SystemRoleCatalog.IsSystemRole(existingRole!.Name) && !RoleConstants.IsPlatformRole(existingRole.Name))
            {
                bool roleIsNotUsed = true;
                var allUsers = await _userManager.Users.ToListAsync();
                foreach (var user in allUsers)
                {
                    if (await _userManager.IsInRoleAsync(user, existingRole.Name!))
                    {
                        roleIsNotUsed = false;
                    }
                }
                if (roleIsNotUsed)
                {
                    existingRole.IsDeleted = true;
                    existingRole.IsActive = false;
                    existingRole.DeletedOn = DateTime.UtcNow;
                    existingRole.DeletedBy = _currentUserService.UserId;
                    await _roleManager.UpdateAsync(existingRole);
                    return await Result<string>.SuccessAsync(string.Format(_localizer["RoleDeleted"], existingRole.Name));
                }
                else
                {
                    return await Result<string>.FailAsync(string.Format(_localizer["RoleNotAllowedDeleteUsed"], existingRole.Name));
                }
            }
            else
            {
                return await Result<string>.FailAsync(string.Format(_localizer["RoleNotAllowedDelete"], existingRole.Name));
            }
        }

        public async Task<Result<List<int>>> GetMenuIdsByRoleIdAsync(string roleId)
        {
            var menuIds = await _unitOfWork.Repository<MenuRole>()
                .GetAllQueryable()
                .Where(x => x.RoleId == roleId)
                .Select(x => x.MenuId)
                .Distinct()
                .ToListAsync();
            return await Result<List<int>>.SuccessAsync(menuIds, _localizer["MessageQuery"], menuIds.Count);
        }

        public async Task<Result<string>> UpdateRoleMenusAsync(UpdateRoleMenusRequest request)
        {
            try
            {
                var role = await _roleManager.FindByIdAsync(request.RoleId);
                if (role == null) return await Result<string>.FailAsync(_localizer["RoleNotFound"]);

                // MenuRole is a join row: only the difference is applied and removed assignments are deleted
                // physically. Soft-deleting every row on each save left the table growing with dead rows.
                var repository = _unitOfWork.Repository<MenuRole>();
                var existing = await repository
                    .GetAllQueryable()
                    .Where(x => x.RoleId == request.RoleId)
                    .ToListAsync();
                var requested = (request.MenuIds ?? []).ToHashSet();
                var kept = new HashSet<int>();

                foreach (var assignment in existing)
                {
                    // Keep one row per requested menu; drop duplicates and unchecked menus.
                    if (requested.Contains(assignment.MenuId) && kept.Add(assignment.MenuId))
                    {
                        continue;
                    }

                    await repository.DeletePermanentAsync(assignment);
                }

                foreach (var menuId in requested.Where(id => !kept.Contains(id)))
                {
                    await repository.AddAsync(new MenuRole
                    {
                        RoleId = request.RoleId,
                        MenuId = menuId
                    });
                }

                await _unitOfWork.Commit(default);
                return await Result<string>.SuccessAsync(_localizer["MenusUpdated"]);
            }
            catch (Exception ex)
            {
                return await Result<string>.FailAsync(ex.Message);
            }
        }
    }

}