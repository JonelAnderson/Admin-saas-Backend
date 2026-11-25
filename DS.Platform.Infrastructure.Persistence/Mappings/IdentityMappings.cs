using DS.Platform.Application.DTOs.Identities.Request;
using DS.Platform.Application.DTOs.Identities.Response;
using DS.Platform.Domain.Entities.Identity;

namespace DS.Platform.Infrastructure.Mappings
{
    /// <summary>Mapeo explícito de usuarios, roles y permisos de rol (sin AutoMapper).</summary>
    public static class IdentityMappings
    {
        /// <summary>
        /// Fila de la lista de usuarios: sucursal y jefe por sus relaciones, estado en texto, MFA y bloqueo vigente (solo si el
        /// bloqueo aún no vence).
        /// </summary>
        public static UserResponse ToResponse(this AdmUser u) => new()
        {
            Id = u.Id,
            UserName = u.UserName,
            FirstName = u.FirstName,
            LastName = u.LastName,
            Email = u.Email,
            ReportUser = u.User?.UserName ?? string.Empty,
            IsActive = u.IsActive,
            UserStatus = u.IsActive ? "Activo" : "Inactivo",
            EmailConfirmed = u.EmailConfirmed,
            PhoneNumber = u.PhoneNumber,
            ProfilePictureDataUrl = u.ProfilePictureDataUrl,
            MfaEnabled = u.TwoFactorEnabled,
            LockedUntil = u.LockoutEnd != null && u.LockoutEnd > DateTimeOffset.UtcNow ? u.LockoutEnd.Value.UtcDateTime : null
        };

        public static UserByIdResponse ToByIdResponse(this AdmUser u) => new()
        {
            Id = u.Id,
            UserName = u.UserName,
            FirstName = u.FirstName,
            LastName = u.LastName,
            Email = u.Email,
            UserId = u.UserId,
            IsActive = u.IsActive,
            EmailConfirmed = u.EmailConfirmed,
            PhoneNumber = u.PhoneNumber,
            ProfilePictureDataUrl = u.ProfilePictureDataUrl
        };

        public static UserSelectResponse ToSelectResponse(this AdmUser u) => new()
        {
            Id = u.Id,
            UserName = u.UserName,
            UserFullName = $"{u.FirstName} {u.LastName}"
        };

        public static RoleResponse ToResponse(this AdmRole r) => new() { Id = r.Id, Name = r.Name, Description = r.Description };

        public static RoleClaimResponse ToResponse(this AdmRoleClaim c) => new()
        {
            Id = c.Id,
            RoleId = c.RoleId,
            Type = c.ClaimType,
            Value = c.ClaimValue,
            Description = c.Description,
            Group = c.Group
        };

        /// <summary>Permiso nuevo de un rol (tipo y valor del claim, descripción y grupo).</summary>
        public static AdmRoleClaim ToEntity(this RoleClaimRequest request) => new()
        {
            Id = request.Id,
            RoleId = request.RoleId!,
            ClaimType = request.Type,
            ClaimValue = request.Value,
            Description = request.Description,
            Group = request.Group
        };
    }
}
