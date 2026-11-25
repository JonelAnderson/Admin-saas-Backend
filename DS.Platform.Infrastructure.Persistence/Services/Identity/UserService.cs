using System.Linq;
using DS.Platform.Infrastructure.Mappings;
using DS.Platform.Application.Configurations;
using DS.Platform.Application.DTOs.Identities.Request;
using DS.Platform.Application.DTOs.Identities.Response;
using DS.Platform.Application.DTOs.Mail.Request;
using DS.Platform.Application.Exceptions;
using DS.Platform.Application.Interfaces.Repositories;
using DS.Platform.Application.Interfaces.Services;
using DS.Platform.Application.Interfaces.Services.Identity;
using DS.Platform.Application.Validators.Features.Files;
using DS.Platform.Application.Validators.Requests.Identity;
using DS.Platform.Domain.Entities;
using DS.Platform.Domain.Entities.Identity;
using DS.Platform.Shared.Commons.Bases.Request;
using DS.Platform.Shared.Constants.Role;
using DS.Platform.Shared.Localization;
using DS.Platform.Shared.Wrapper;
using Hangfire;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text;
using System.Text.Encodings.Web;

namespace DS.Platform.Infrastructure.Services.Identity
{
    public class UserService : IUserService
    {
        private readonly UserManager<AdmUser> _userManager;
        private readonly RoleManager<AdmRole> _roleManager;
        private readonly IMailService _mailService;
        private readonly RegisterRequestValidator _validationRules;

        private readonly ICurrentUserService _currentUserService;
        private readonly IUnitOfWork<int> _unitOfWork;

        private readonly IUploadService _uploadService;
        private readonly IFileUploadValid _fileUploadValid;
        private readonly IStringLocalizer<ServerCommonResources> _localizer;
        private readonly ISessionStampService _sessions;
        private readonly ITrustedDeviceService _devices;
        private readonly AppConfiguration _app;
        private readonly IHostEnvironment _environment;
        private readonly ILogger<UserService> _logger;

        /// <summary>Respuesta de "olvidé mi contraseña", exista o no el correo (no revela qué correos están registrados).</summary>
        public const string ForgotPasswordMessage = "Si el correo está registrado, le enviaremos un enlace para restablecer la contraseña.";

        public UserService(
            UserManager<AdmUser> userManager,
            RoleManager<AdmRole> roleManager,
            IMailService mailService,

            ICurrentUserService currentUserService,
            RegisterRequestValidator validationRules,
            IUnitOfWork<int> unitOfWork,
            IFileUploadValid fileUploadValid,
            IUploadService uploadService,
            IStringLocalizer<ServerCommonResources> localizer,
            ISessionStampService sessions,
            IOptions<AppConfiguration> app,
            IHostEnvironment environment,
            ITrustedDeviceService devices,
            ILogger<UserService>? logger = null)
        {
            _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<UserService>.Instance;
            _devices = devices;
            _sessions = sessions;
            _app = app.Value;
            _environment = environment;
            _userManager = userManager;
            _roleManager = roleManager;
            _mailService = mailService;
            _currentUserService = currentUserService;
            _validationRules = validationRules;
            _unitOfWork = unitOfWork;
            _fileUploadValid = fileUploadValid;
            _uploadService = uploadService;
            _localizer = localizer;
        }

        public async Task<Result<List<UserResponse>>> GetAllAsync(BaseFiltersRequest filters)
        {

            // Obtén la lista de usuarios como IQueryable para poder aplicar filtros
            // SuperAdmin: todos; los demás: todos menos los SuperAdmin.
            var query = (await UserAdminScope.VisibleAsync(_userManager, _currentUserService))
                .AsNoTracking()
                .AsQueryable();

            // Aplica filtros si los hay
            if (!string.IsNullOrEmpty(filters.TextFilter))
            {

                string textFilter = filters.TextFilter.ToLower();
                query = query.Where(x =>
                    x.UserName!.ToLower().Contains(textFilter) ||
                    x.FirstName!.ToLower().Contains(textFilter) ||
                    x.LastName!.ToLower().Contains(textFilter));
              
            }

            // Filtro por estado del usuario (activo/inactivo)
            if (filters.StateFilter is not null)
            {
                bool boolValue = Convert.ToBoolean(filters.StateFilter);

                query = query.Where(x => x.IsActive.Equals(boolValue));
              
            }

           

            // Obtén el total de registros sin paginación (opcional, si necesitas paginación)
            int totalRecords = await query.CountAsync();

            // Paginación: ordenar SIEMPRE antes de Skip / Take (Id descendente; cursor LastStringValue si viene).
            query = query.OrderByDescending(x => x.Id);
            if (filters.NumPage > 0 && filters.NumRecordsPage > 0)
            {
                if (!string.IsNullOrEmpty(filters.LastStringValue))
                {
                    query = query.Where(x => x.Id.CompareTo(filters.LastStringValue) < 0);
                }
                else if (filters.NumPage > 1)
                {
                    query = query.Skip((filters.NumPage - 1) * filters.NumRecordsPage);
                }
                query = query.Take(filters.NumRecordsPage);
            }

            // Proyecta los resultados a UserResponse (con el estado del MFA y del bloqueo, Fase 12a)
            var rows = await query.Select(user => new
            {
                LockoutEnd = user.LockoutEnd,
                Response = new UserResponse
            {
                Id = user.Id,
                UserName = user.UserName,
                FirstName = user.FirstName,
                LastName = user.LastName,
                Email = user.Email,
                ReportUser = user.User.UserName!,
                IsActive = user.IsActive,
                UserStatus = user.IsActive ? "Activo" : "Inactivo",
                PhoneNumber = user.PhoneNumber,
                ProfilePictureDataUrl = user.ProfilePictureDataUrl,
                MfaEnabled = user.TwoFactorEnabled,
            }
            }).ToListAsync();
            var now = DateTimeOffset.UtcNow;
            var result = rows.Select(row =>
            {
                row.Response.LockedUntil = row.LockoutEnd > now ? row.LockoutEnd.Value.UtcDateTime : null;
                return row.Response;
            }).ToList();

            if (result.Any())
            {
                return await Result<List<UserResponse>>.SuccessAsync(result, _localizer["MessageQuery"], totalRecords);
            }
            else
            {
                return await Result<List<UserResponse>>.FailAsync(new List<UserResponse>(), _localizer["MessageQueryEmpty"]);
            }

        }

       



        public async Task<Result<List<UserSelectResponse>>> GetUserSelectAsync()
        {
            var users = await (await UserAdminScope.VisibleAsync(_userManager, _currentUserService))
                .Where(x => x.IsActive && !x.IsDeleted).ToListAsync();
            var result = users.Select(x => x.ToSelectResponse()).ToList();
            return await Result<List<UserSelectResponse>>.SuccessAsync(result);
        }


        private async Task<string> GetUserIdAsync(string userName)
        {
            if (!string.IsNullOrEmpty(userName))
            {
                var user = await _userManager.FindByNameAsync(userName);
                return user?.Id ?? string.Empty;
            }
            return string.Empty;
        }

        public async Task<IResult> RegisterAsync(RegisterRequest request)
        {
            try
            {

                var validationResult = await _validationRules.ValidateAsync(request);

                if (!validationResult.IsValid)
                {
                    return await Result.FailAsync(validationResult.Errors);
                    //return await Result.FailAsync(string.Format("Errores de validaciones."));
                }


                if (!string.IsNullOrEmpty(request.UserId) && !await UserAdminScope.IsVisibleAsync(_userManager, _currentUserService, request.UserId))
                {
                    return await Result.FailAsync(UserAdminScope.ReportsToDeniedMessage);
                }

                var userWithSameUserName = await _userManager.FindByNameAsync(request.UserName!);

                if (userWithSameUserName != null)
                {
                    return await Result.FailAsync(string.Format(_localizer["UsernameTaken"], request.UserName));
                }
                var user = new AdmUser
                {
                    Email = request.Email,
                    FirstName = request.FirstName!,
                    LastName = request.LastName!,
                    UserName = request.UserName,
                    PhoneNumber = request.PhoneNumber,
                    UserId = request.UserId,
                    IsActive = request.IsActive,
                    EmailConfirmed = request.EmailConfirmed
                };

                if (!string.IsNullOrWhiteSpace(request.PhoneNumber))
                {
                    var userWithSamePhoneNumber = await _userManager.Users.FirstOrDefaultAsync(x => x.PhoneNumber == request.PhoneNumber);
                    if (userWithSamePhoneNumber != null)
                    {
                        return await Result.FailAsync(string.Format(_localizer["PhoneNumberInUse"], request.PhoneNumber));
                    }
                }

                // La foto se sube después del alta (POST api/identity/account/profile-picture/{id}), no en este JSON.

                var result = await _userManager.CreateAsync(user, request.Password!);
                if (result.Succeeded)
                {
                    await _userManager.AddToRoleAsync(user, RoleConstants.BasicRole);
                    return await Result<string>.SuccessAsync(user.Id, string.Format(_localizer["UserRegistered"], user.UserName));
                }
                else if (result.Errors.Any(e => e.Code == nameof(IdentityErrorDescriber.DuplicateEmail)))
                {
                    return await Result.FailAsync(string.Format(_localizer["EmailInUse"], request.Email));
                }
                else
                {
                    // Política de contraseña u otra validación de Identity: se informa el motivo real.
                    return await Result.FailAsync(result.Errors.Select(e => e.Description).ToList());
                }


                //var userWithSameEmail = await _userManager.FindByEmailAsync(request.Email);
                //if (userWithSameEmail == null)
                //{
                //    var result = await _userManager.CreateAsync(user, request.Password);
                //    if (result.Succeeded)
                //    {
                //        await _userManager.AddToRoleAsync(user, RoleConstants.BasicRole);
                //        if (!request.AutoConfirmEmail)
                //        {
                //            var verificationUri = await SendVerificationEmail(user, origin);
                //            var mailRequest = new MailRequest
                //            {
                //                From = "mail@codewithmukesh.com",
                //                To = user.Email,
                //                Body = string.Format("Please confirm your account by <a href='{0}'>clicking here</a>.", verificationUri),
                //                Subject = "Confirm Registration"
                //            };
                //            BackgroundJob.Enqueue(() => _mailService.SendAsync(mailRequest));
                //            return await Result<string>.SuccessAsync(user.Id, string.Format("User {0} Registered. Please check your Mailbox to verify!", user.UserName));
                //        }
                //        return await Result<string>.SuccessAsync(user.Id, string.Format("User {0} Registered.", user.UserName));
                //    }
                //    else
                //    {
                //        return await Result.FailAsync(result.Errors.Select(a => a.Description.ToString()).ToList());
                //    }
                //}
                //else
                //{
                //    return await Result.FailAsync(string.Format("Email {0} is already registered.", request.Email));
                //}
            }
            catch (Exception)
            {

                return await Result.FailAsync(_localizer["SomethingWentWrong"]);
            }
            
        }

        private async Task<string> SendVerificationEmail(AdmUser user, string origin)
        {
            var code = await _userManager.GenerateEmailConfirmationTokenAsync(user);
            code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(code));
            var route = "api/identity/user/confirm-email/";
            var endpointUri = new Uri(string.Concat($"{origin}/", route));
            var verificationUri = QueryHelpers.AddQueryString(endpointUri.ToString(), "userId", user.Id);
            verificationUri = QueryHelpers.AddQueryString(verificationUri, "code", code);
            return verificationUri;
        }

        public async Task<IResult<UserByIdResponse>> GetAsync(string userId)
        {
            var user = await (await UserAdminScope.VisibleAsync(_userManager, _currentUserService)).Where(u => u.Id == userId).FirstOrDefaultAsync();
            if (user is null)
            {
                return await Result<UserByIdResponse>.FailAsync(_localizer["UserNotFound"]);
            }
            var result = user.ToByIdResponse();
            return await Result<UserByIdResponse>.SuccessAsync(result);
        }

        public async Task<IResult> ToggleUserStatusAsync(ToggleUserStatusRequest request)
        {
            var user = await _userManager.Users.Where(u => u.Id == request.UserId).FirstOrDefaultAsync();
            if (user is null || !await UserAdminScope.CanManageAsync(_userManager, _currentUserService, user))
            {
                return await Result.FailAsync(UserAdminScope.DeniedMessage);
            }
            // El superadministrador no se desactiva (el administrador de empresa sí, con Users.ChangeState).
            var isAdmin = user is not null && (await _userManager.GetRolesAsync(user)).Any(RoleConstants.IsPlatformRole);
            if (isAdmin)
            {
                return await Result.FailAsync(_localizer["MessageChangeProfileAdminState"]);
            }
            if (user != null)
            {
                user.IsActive = request.ActivateUser;
                if (!user.IsActive)
                {
                    // Desactivado: sin refresh token y sus JWT abiertos dejan de valer en el siguiente request.
                    user.RefreshToken = null;
                }
                var identityResult = await _userManager.UpdateAsync(user);
                _sessions.Invalidate(user.Id);
                if (!user.IsActive)
                {
                    await _devices.RevokeAllAsync(user.Id);
                }
            }
            return await Result.SuccessAsync(user?.IsActive == true ? _localizer["MessageActive"] : _localizer["MessageInactive"] );
        }

        public async Task<IResult<UserRolesResponse>> GetRolesAsync(string userId)
        {
            var viewModel = new List<UserRoleModel>();
            var user = await _userManager.FindByIdAsync(userId);
            if (user is null || !await UserAdminScope.CanManageAsync(_userManager, _currentUserService, user))
            {
                return await Result<UserRolesResponse>.FailAsync(UserAdminScope.DeniedMessage);
            }
            var roles = await _roleManager.Roles.ToListAsync();

            foreach (var role in roles)
            {
                var userRolesViewModel = new UserRoleModel
                {
                    RoleName = role.Name,
                    RoleDescription = role.Description
                };
                if (await _userManager.IsInRoleAsync(user!, role.Name!))
                {
                    userRolesViewModel.Selected = true;
                }
                else
                {
                    userRolesViewModel.Selected = false;
                }
                viewModel.Add(userRolesViewModel);
            }
            var result = new UserRolesResponse { UserRoles = viewModel };
            return await Result<UserRolesResponse>.SuccessAsync(result);
        }

        public async Task<IResult> UpdateRolesAsync(UpdateUserRolesRequest request)
        {
            var user = await _userManager.FindByIdAsync(request.UserId!);
            if (user is null || !await UserAdminScope.CanManageAsync(_userManager, _currentUserService, user))
            {
                return await Result.FailAsync(UserAdminScope.DeniedMessage);
            }

            var roles = await _userManager.GetRolesAsync(user);
            // Cada rol pedido se resuelve al rol guardado: Identity asigna por nombre normalizado (sin distinguir mayúsculas),
            // así que los controles de abajo tienen que mirar el nombre real ("superadmin" es SuperAdmin), no el texto recibido.
            var selectedRoles = new List<string>();
            foreach (var requested in request.UserRoles!.Where(x => x.Selected))
            {
                var stored = string.IsNullOrWhiteSpace(requested.RoleName) ? null : await _roleManager.FindByNameAsync(requested.RoleName.Trim());
                if (stored?.Name is null)
                {
                    return await Result.FailAsync($"El rol '{requested.RoleName}' no existe.");
                }
                if (!selectedRoles.Contains(stored.Name, StringComparer.Ordinal))
                {
                    selectedRoles.Add(stored.Name);
                }
            }

            var currentUser = await _userManager.FindByIdAsync(_currentUserService.UserId);
            var currentRoles = currentUser is null ? new List<string>() : (await _userManager.GetRolesAsync(currentUser)).ToList();
            var currentIsSuperAdmin = currentRoles.Any(RoleConstants.IsPlatformRole);
            var currentIsAdministrator = currentIsSuperAdmin || currentRoles.Contains(RoleConstants.AdministratorRole);

            // Cambia (da o quita) el rol si estaba seleccionado y no lo tenía, o lo tenía y ya no está seleccionado.
            bool Changes(Func<string, bool> isRole)
                => selectedRoles.Any(isRole) != roles.Any(isRole);

            // SuperAdmin solo lo da o quita otro SuperAdmin, y nunca a sí mismo (no puede quedarse sin acceso de plataforma).
            if (Changes(RoleConstants.IsPlatformRole)
                && (!currentIsSuperAdmin || string.Equals(user.Id, currentUser?.Id, StringComparison.Ordinal)))
            {
                return await Result.FailAsync(_localizer["NotAllowedAdminRoleChange"]);
            }
            // Administrator solo lo da o quita un SuperAdmin o un Administrator.
            if (Changes(r => r == RoleConstants.AdministratorRole) && !currentIsAdministrator)
            {
                return await Result.FailAsync(_localizer["NotAllowedAdminRoleChange"]);
            }

            var result = await _userManager.RemoveFromRolesAsync(user, roles);
            if (result.Succeeded)
            {
                result = await _userManager.AddToRolesAsync(user, selectedRoles);
            }
            if (!result.Succeeded)
            {
                return await Result.FailAsync(result.Errors.Select(e => e.Description).ToList());
            }
            // Los permisos viajan en el JWT: nuevo stamp para que sus tokens abiertos se rechacen. Se conserva el refresh token,
            // así el frontend renueva solo y recibe los permisos nuevos sin volver al login.
            await _userManager.UpdateSecurityStampAsync(user);
            _sessions.Invalidate(user.Id);
            return await Result.SuccessAsync(_localizer["RolesUpdated"]);
        }

        public async Task<IResult<string>> ConfirmEmailAsync(string userId, string code)
        {
            var user = await _userManager.FindByIdAsync(userId);
            code = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(code));
            var result = await _userManager.ConfirmEmailAsync(user!, code);
            if (result.Succeeded)
            {
                return await Result<string>.SuccessAsync(user!.Id, string.Format(_localizer["AccountConfirmed"], user.Email));
            }
            else
            {
                throw new ApiException(string.Format(_localizer["ErrorConfirmingEmail"], user!.Email));
            }
        }

        public async Task<IResult> ForgotPasswordAsync(ForgotPasswordRequest request)
        {
            // El enlace apunta siempre al frontend configurado; en Development, si falta, al de `ng serve`.
            var origin = (_app.ApplicationUrl ?? string.Empty).Trim().TrimEnd('/');
            if (string.IsNullOrEmpty(origin) && _environment.IsDevelopment())
            {
                origin = "https://localhost:4210"; // ng serve de la consola con ssl (angular.json); la API (5011) no sirve la SPA
            }
            if (string.IsNullOrEmpty(origin))
            {
                // Sin URL del frontend no se puede armar el enlace: se avisa en el log, nunca al cliente.
                _logger.LogWarning("Recuperación de contraseña: falta AppConfiguration:ApplicationUrl; no se envió el enlace.");
                return await Result.SuccessAsync(ForgotPasswordMessage);
            }

            var user = string.IsNullOrWhiteSpace(request.Email) ? null : await _userManager.FindByEmailAsync(request.Email);
            // Sin exigir EmailConfirmed: no hay registro propio ni correo de confirmación; el correo lo registra el administrador
            // (los usuarios creados desde la pantalla quedaban sin confirmar y nunca recibían el enlace).
            if (user == null || !user.IsActive || string.IsNullOrWhiteSpace(user.Email))
            {
                // La misma respuesta que cuando sí existe: no revela qué correos están registrados (solo el log del servidor lo dice).
                _logger.LogInformation("Recuperación de contraseña para {Email}: {Reason}; no se envía correo.", MaskEmail(request.Email),
                    user == null ? "no hay usuario con ese correo" : !user.IsActive ? "el usuario está desactivado" : "el usuario no tiene correo");
                return await Result.SuccessAsync(ForgotPasswordMessage);
            }
            // For more information on how to enable account confirmation and password reset please
            // visit https://go.microsoft.com/fwlink/?LinkID=532713
            var code = PasswordResetToken.Encode(await _userManager.GeneratePasswordResetTokenAsync(user));
            var route = "account/reset-password";
            var endpointUri = new Uri(string.Concat($"{origin}/", route));
            var passwordResetURL = QueryHelpers.AddQueryString(endpointUri.ToString(), "Token", code);
            var mailRequest = new MailRequest
            {
                Body = string.Format(
                    "<p>Hola {0}:</p><p>Recibimos una solicitud para restablecer la contraseña de su cuenta. Para elegir una nueva, "
                    + "<a href='{1}'>haga clic aquí</a>.</p><p>Si usted no lo pidió, ignore este correo: su contraseña no cambia.</p>",
                    HtmlEncoder.Default.Encode(GreetingName(user)), HtmlEncoder.Default.Encode(passwordResetURL)),
                Subject = "Restablecer su contraseña",
                // Al correo guardado y confirmado, nunca al texto que se escribió: la búsqueda normaliza (mayúsculas,
                // Unicode) y un correo "equivalente" escrito por otro recibiría el enlace de esta cuenta.
                To = user.Email!
            };
            var jobId = BackgroundJob.Enqueue(() => _mailService.SendAsync(mailRequest));
            _logger.LogInformation("Recuperación de contraseña: correo para {Email} (usuario {UserId}) en cola, trabajo Hangfire {JobId}.",
                MaskEmail(user.Email), user.Id, jobId);
            return await Result.SuccessAsync(ForgotPasswordMessage);
        }

        /// <summary>Nombre del saludo: nombres y apellidos; si no tiene, el usuario.</summary>
        internal static string GreetingName(AdmUser user)
        {
            var full = string.Join(' ', new[] { user.FirstName, user.LastName }.Where(p => !string.IsNullOrWhiteSpace(p)).Select(p => p!.Trim()));
            return full.Length > 0 ? full : user.UserName ?? string.Empty;
        }

        /// <summary>Correo enmascarado para el log ("ju***@gmail.com"): sirve para diagnosticar sin guardar el dato completo.</summary>
        internal static string MaskEmail(string? email)
        {
            var text = email?.Trim() ?? string.Empty;
            var at = text.IndexOf('@');
            if (at <= 0)
                return text.Length == 0 ? "(vacío)" : "***";
            return string.Concat(text.AsSpan(0, Math.Min(2, at)), "***", text.AsSpan(at));
        }

        public async Task<IResult> ResetPasswordAsync(ResetPasswordRequest request)
        {
            const string invalidLink = "El enlace no es válido o ya venció, o el correo no corresponde. Solicite un enlace nuevo.";
            var user = string.IsNullOrWhiteSpace(request.Email) ? null : await _userManager.FindByEmailAsync(request.Email.Trim());
            var token = PasswordResetToken.TryDecode(request.Token);
            if (user == null || !user.IsActive || token == null)
            {
                // El mismo mensaje exista o no el correo: no revela qué cuentas hay.
                return await Result.FailAsync(invalidLink);
            }

            var result = await _userManager.ResetPasswordAsync(user, token, request.Password!);
            if (result.Succeeded)
            {
                // Nuevo security stamp (lo cambia Identity) y sin refresh token: las sesiones abiertas no se renuevan.
                user.RefreshToken = null;
                await _userManager.UpdateAsync(user);
                _sessions.Invalidate(user.Id);
                await _devices.RevokeAllAsync(user.Id);
                return await Result.SuccessAsync("Su contraseña se cambió. Ya puede iniciar sesión con la nueva.");
            }
            // Token vencido o ya usado: mensaje genérico; reglas de contraseña: las del describer en español.
            if (result.Errors.Any(e => e.Code == nameof(IdentityErrorDescriber.InvalidToken)))
                return await Result.FailAsync(invalidLink);
            return await Result.FailAsync(result.Errors.Select(e => e.Description).ToList());
        }

        public async Task<int> GetCountAsync()
        {
            var count = await _userManager.Users.CountAsync();
            return count;
        }

        public async Task<List<UserByIdResponse>> GetUsersByIdsAsync(List<string> userIds)
        {
            var users = await _userManager.Users.Where(u => userIds.Contains(u.Id)).ToListAsync();
            var response = users.Select(x => x.ToByIdResponse()).ToList();
            return response;
        }
    }
}
