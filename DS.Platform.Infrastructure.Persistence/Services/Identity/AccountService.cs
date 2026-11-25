using DS.Platform.Application.DTOs.Identities.Request;
using DS.Platform.Application.DTOs.Identities.Response;
using DS.Platform.Application.Interfaces.Services;
using DS.Platform.Application.Interfaces.Services.Account;
using DS.Platform.Application.Interfaces.Services.Identity;
using DS.Platform.Application.Validators.Features.Files;
using DS.Platform.Application.Interfaces.Repositories;
using DS.Platform.Domain.Entities;
using DS.Platform.Domain.Entities.Identity;
using DS.Platform.Shared.Localization;
using DS.Platform.Shared.Static;
using DS.Platform.Shared.Wrapper;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using IFormFile = Microsoft.AspNetCore.Http.IFormFile;

namespace DS.Platform.Infrastructure.Services.Identity
{
    public class AccountService : IAccountService
    {
        private readonly UserManager<AdmUser> _userManager;
        private readonly IUploadService _uploadService;
        private readonly IFileUploadValid _fileUploadValid;
        private readonly IStringLocalizer<ServerCommonResources> _localizer;
        private readonly ICurrentUserService _currentUser;
        private readonly ISessionStampService _sessions;
        private readonly ITrustedDeviceService _devices;
        private readonly IUnitOfWork<int> _unitOfWork;

        public AccountService(
            UserManager<AdmUser> userManager,
            IUploadService uploadService,
            IFileUploadValid fileUploadValid,
            IStringLocalizer<ServerCommonResources> localizer,
            ICurrentUserService currentUser,
            ISessionStampService sessions,
            IUnitOfWork<int> unitOfWork,
            ITrustedDeviceService devices)
        {
            _devices = devices;
            _unitOfWork = unitOfWork;
            _currentUser = currentUser;
            _sessions = sessions;
            _userManager = userManager;
            _uploadService = uploadService;
            _fileUploadValid = fileUploadValid;
            _localizer = localizer;
        }

        public async Task<IResult> ChangePasswordAsync(ChangePasswordRequest model)
        {
            // Fase 12a: only the signed-in user changes their own password; the ProfileId sent by the client is ignored.
            var user = string.IsNullOrEmpty(_currentUser.UserId) ? null : await this._userManager.FindByIdAsync(_currentUser.UserId);
            if (user == null)
            {
                return await Result.FailAsync(_localizer["UserNotFound"]);
            }

            var identityResult = await this._userManager.ChangePasswordAsync(
                user,
                model.Password!,
                model.NewPassword!);
            var errors = identityResult.Errors.Select(e => e.Description.ToString()).ToList();
            if (identityResult.Succeeded)
            {
                // Fase 12a: Identity ya cambió el security stamp; sin refresh token las sesiones abiertas no se renuevan.
                user.RefreshToken = null;
                await _userManager.UpdateAsync(user);
                // Los JWT emitidos con el stamp anterior (todos los dispositivos) dejan de valer en el siguiente request.
                _sessions.Invalidate(user.Id);
                // Y los dispositivos recordados vuelven a pedir el código.
                await _devices.RevokeAllAsync(user.Id);
            }
            return identityResult.Succeeded ? await Result.SuccessAsync() : await Result.FailAsync(errors);
        }

        public async Task<IResult> UpdateProfileAsync(UpdateProfileRequest request)
        {
            // La administración de usuarios edita a otro usuario: solo de su empresa, con una sucursal y un jefe de su empresa.
            var target = string.IsNullOrEmpty(request.ProfileId) ? null : await _userManager.FindByIdAsync(request.ProfileId);
            if (target is null)
            {
                return await Result.FailAsync(_localizer["UserNotFound"]);
            }
            if (!await UserAdminScope.CanManageAsync(_userManager, _currentUser, target))
            {
                return await Result.FailAsync(UserAdminScope.DeniedMessage);
            }
            if (!string.IsNullOrEmpty(request.UserId) && request.UserId != target.UserId
                && !await UserAdminScope.IsVisibleAsync(_userManager, _currentUser, request.UserId))
            {
                return await Result.FailAsync(UserAdminScope.ReportsToDeniedMessage);
            }

            if (!string.IsNullOrWhiteSpace(request.PhoneNumber))
            {
                var userWithSamePhoneNumber = await _userManager.Users.FirstOrDefaultAsync(x => x.PhoneNumber == request.PhoneNumber && x.Id != request.ProfileId);
                if (userWithSamePhoneNumber != null)
                {
                    return await Result.FailAsync(string.Format(_localizer["PhoneNumberInUse"], request.PhoneNumber));
                }
            }

            var userWithSameEmail = await _userManager.FindByEmailAsync(request.Email!);
            if (userWithSameEmail == null || userWithSameEmail.Id == request.ProfileId)
            {
                var user = await _userManager.FindByIdAsync(request.ProfileId);
                if (user == null)
                {
                    return await Result.FailAsync(_localizer["UserNotFound"]);
                }
                user.UserName = request.UserLogin;
                user.FirstName = request.FirstName;
                user.LastName = request.LastName;
                user.PhoneNumber = request.PhoneNumber;
                user.UserId = request.UserId;
                // El correo del formulario también se guarda (antes solo se validaba). Directo y no con SetEmailAsync, que lo
                // dejaría sin confirmar; UpdateAsync actualiza el correo normalizado y valida que no se repita.
                if (!string.IsNullOrWhiteSpace(request.Email))
                {
                    user.Email = request.Email.Trim();
                }
                // La foto se sube aparte (POST profile-picture/{userId}, multipart), no en este JSON.

                var identityResult = await _userManager.UpdateAsync(user);
                var errors = identityResult.Errors.Select(e => e.Description.ToString()).ToList();
                // Sin RefreshSignInAsync: quien edita es un administrador (con JWT); refrescar la sesión de cookie del usuario
                // editado no corresponde.
                return identityResult.Succeeded ? await Result.SuccessAsync(_localizer["MessageUpdate"]) : await Result.FailAsync(errors);
            }
            else
            {
                return await Result.FailAsync(string.Format(_localizer["EmailInUse"], request.Email));
            }
        }

        public async Task<IResult<CurrentUserProfileResponse>> GetCurrentProfileAsync()
        {
            var user = string.IsNullOrEmpty(_currentUser.UserId) ? null : await _userManager.FindByIdAsync(_currentUser.UserId);
            if (user == null)
            {
                return await Result<CurrentUserProfileResponse>.FailAsync(_localizer["UserNotFound"]);
            }
            var fullName = $"{user.FirstName} {user.LastName}".Trim();
            return await Result<CurrentUserProfileResponse>.SuccessAsync(new CurrentUserProfileResponse
            {
                Id = user.Id,
                UserName = user.UserName,
                FirstName = user.FirstName,
                LastName = user.LastName,
                FullName = fullName.Length > 0 ? fullName : user.UserName ?? string.Empty,
                Email = user.Email,
                ProfilePictureUrl = string.IsNullOrWhiteSpace(user.ProfilePictureDataUrl) ? null : user.ProfilePictureDataUrl
            });
        }

        public async Task<IResult<string>> GetProfilePictureAsync(string userId)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                return await Result<string>.FailAsync(_localizer["UserNotFound"]);
            }
            // La URL abre el archivo sin login: solo la propia o la de un usuario que el administrador gestiona (como al subirla).
            if (user.Id != _currentUser.UserId && !await UserAdminScope.CanManageAsync(_userManager, _currentUser, user))
            {
                return await Result<string>.FailAsync(UserAdminScope.DeniedMessage);
            }
            return await Result<string>.SuccessAsync(data: user.ProfilePictureDataUrl!);
        }

        /// <summary>
        /// Foto de perfil de un usuario (la sube la administración de usuarios después de crear o editar). Solo un usuario que
        /// el administrador puede gestionar (UserAdminScope), y solo una imagen válida; reemplaza la anterior.
        /// </summary>
        public async Task<IResult<string>> UpdateProfilePictureAsync(IFormFile request, string userId)
        {
            var user = string.IsNullOrEmpty(userId) ? null : await _userManager.FindByIdAsync(userId);
            if (user == null) return await Result<string>.FailAsync(message: _localizer["UserNotFound"]);
            if (user.Id != _currentUser.UserId && !await UserAdminScope.CanManageAsync(_userManager, _currentUser, user))
            {
                return await Result<string>.FailAsync(UserAdminScope.DeniedMessage);
            }
            if (request is null)
            {
                return await Result<string>.FailAsync("Seleccione una imagen.");
            }
            var validation = await _fileUploadValid.ValidationUrlImagen(request);
            if (!validation.Succeeded)
            {
                return await Result<string>.FailAsync(validation.Messages);
            }
            var filePath = string.IsNullOrEmpty(user.ProfilePictureDataUrl)
                ? await _uploadService.SaveFile(FileFolderContainers.USERS, request)
                : await _uploadService.EditFile(FileFolderContainers.USERS, request, user.ProfilePictureDataUrl);
            user.ProfilePictureDataUrl = filePath;
            var identityResult = await _userManager.UpdateAsync(user);
            var errors = identityResult.Errors.Select(e => e.Description.ToString()).ToList();
            return identityResult.Succeeded ? await Result<string>.SuccessAsync(data: filePath) : await Result<string>.FailAsync(errors);
        }
    }
}