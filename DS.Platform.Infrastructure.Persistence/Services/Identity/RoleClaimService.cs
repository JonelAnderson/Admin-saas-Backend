using DS.Platform.Infrastructure.Mappings;
using DS.Platform.Application.DTOs.Identities.Request;
using DS.Platform.Application.DTOs.Identities.Response;
using DS.Platform.Application.Interfaces.Services;
using DS.Platform.Application.Interfaces.Services.Identity;
using DS.Platform.Domain.Entities;
using DS.Platform.Domain.Entities.Identity;
using DS.Platform.Infrastructure.Persistence.Contexts;
using DS.Platform.Shared.Wrapper;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Localization;
using DS.Platform.Shared.Localization;

namespace DS.Platform.Infrastructure.Services.Identity
{
    public class RoleClaimService : IRoleClaimService
    {
        private readonly ICurrentUserService _currentUserService;
        private readonly ApplicationDbContext _db;
        private readonly IStringLocalizer<ServerCommonResources> _localizer;
        private readonly IUserPermissionCache _permissionCache;

        public RoleClaimService(
            ICurrentUserService currentUserService,
            ApplicationDbContext db,
            IStringLocalizer<ServerCommonResources> localizer,
            IUserPermissionCache permissionCache)
        {
            _permissionCache = permissionCache;
            _currentUserService = currentUserService;
            _db = db;
            _localizer = localizer;
        }

        public async Task<Result<List<RoleClaimResponse>>> GetAllAsync()
        {
            var roleClaims = await _db.RoleClaims.ToListAsync();
            var roleClaimsResponse = roleClaims.Select(x => x.ToResponse()).ToList();
            return await Result<List<RoleClaimResponse>>.SuccessAsync(roleClaimsResponse);
        }

        public async Task<int> GetCountAsync()
        {
            var count = await _db.RoleClaims.CountAsync();
            return count;
        }

        public async Task<Result<RoleClaimResponse>> GetByIdAsync(int id)
        {
            var roleClaim = await _db.RoleClaims
                .SingleOrDefaultAsync(x => x.Id == id);
            var roleClaimResponse = roleClaim?.ToResponse();
            return await Result<RoleClaimResponse>.SuccessAsync(roleClaimResponse);
        }

        public async Task<Result<List<RoleClaimResponse>>> GetAllByRoleIdAsync(string roleId)
        {
            var roleClaims = await _db.RoleClaims
                .Include(x => x.Role)
                .Where(x => x.RoleId == roleId)
                .ToListAsync();
            var roleClaimsResponse = roleClaims.Select(x => x.ToResponse()).ToList();
            return await Result<List<RoleClaimResponse>>.SuccessAsync(roleClaimsResponse);
        }

        public async Task<Result<string>> SaveAsync(RoleClaimRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.RoleId))
            {
                return await Result<string>.FailAsync(_localizer["RoleRequired"]);
            }

            if (request.Id == 0)
            {
                var existingRoleClaim =
                    await _db.RoleClaims
                        .SingleOrDefaultAsync(x =>
                            x.RoleId == request.RoleId && x.ClaimType == request.Type && x.ClaimValue == request.Value);
                if (existingRoleClaim != null)
                {
                    return await Result<string>.FailAsync(_localizer["RoleClaimExists"]);
                }
                var roleClaim = request.ToEntity();
                await _db.RoleClaims.AddAsync(roleClaim);
                await _db.SaveChangesAsync(_currentUserService.UserId);
                _permissionCache.InvalidateAll();
                return await Result<string>.SuccessAsync(string.Format(_localizer["RoleClaimCreated"], request.Value));
            }
            else
            {
                var existingRoleClaim =
                    await _db.RoleClaims
                        .Include(x => x.Role)
                        .SingleOrDefaultAsync(x => x.Id == request.Id);
                if (existingRoleClaim == null)
                {
                    return await Result<string>.SuccessAsync(_localizer["RoleClaimNotFound"]);
                }
                else
                {
                    existingRoleClaim.ClaimType = request.Type;
                    existingRoleClaim.ClaimValue = request.Value;
                    existingRoleClaim.Group = request.Group;
                    existingRoleClaim.Description = request.Description;
                    existingRoleClaim.RoleId = request.RoleId;
                    _db.RoleClaims.Update(existingRoleClaim);
                    await _db.SaveChangesAsync(_currentUserService.UserId);
                    _permissionCache.InvalidateAll();
                    return await Result<string>.SuccessAsync(string.Format(_localizer["RoleClaimUpdated"], request.Value, existingRoleClaim.Role!.Name));
                }
            }
        }

        public async Task<Result<string>> DeleteAsync(int id)
        {
            var existingRoleClaim = await _db.RoleClaims
                .Include(x => x.Role)
                .FirstOrDefaultAsync(x => x.Id == id);
            if (existingRoleClaim != null)
            {
                _db.RoleClaims.Remove(existingRoleClaim);
                await _db.SaveChangesAsync(_currentUserService.UserId);
                _permissionCache.InvalidateAll();
                return await Result<string>.SuccessAsync(string.Format(_localizer["RoleClaimDeleted"], existingRoleClaim.ClaimValue, existingRoleClaim.Role!.Name));
            }
            else
            {
                return await Result<string>.FailAsync(_localizer["RoleClaimNotFound"]);
            }
        }
    }
}