using DS.Platform.Application.Interfaces.Services;
using DS.Platform.Application.Interfaces.Services.Identity;
using DS.Platform.Domain.Entities.Identity;
using DS.Platform.Shared.Constants.Application;
using DS.Platform.Shared.Constants.Permission;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.SignalR;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace DS.Platform.WebApi.Hubs
{
    /// <summary>Solo usuarios con JWT válido (incluye el chequeo del security stamp). Cerrar sesiones de otros exige permiso.</summary>
    [Authorize]
    public class SignalRHub : Hub
    {
        private static readonly ConcurrentDictionary<string, List<string>> _userConnections = new();
        private readonly UserManager<AdmUser> _userManager;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly IDateTimeService _dateTimeService;
        private readonly IRoleService _roleService;
        private readonly ISessionStampService _sessions;
        public SignalRHub(UserManager<AdmUser> userManager,
        IHttpContextAccessor httpContextAccessor,
        IDateTimeService dateTimeService,
        IRoleService roleService,
        ISessionStampService sessions)
        {
            _sessions = sessions;
            _userManager = userManager;
            _httpContextAccessor = httpContextAccessor;
            _dateTimeService = dateTimeService;
            _roleService = roleService;
        }

        public override async Task OnConnectedAsync()
        {
            //var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            //if (!string.IsNullOrEmpty(userId))
            //{
            //    await Groups.AddToGroupAsync(Context.ConnectionId, userId);
            //}
            //await base.OnConnectedAsync();

            var userId = Context.UserIdentifier; // Asume que tienes el UserId como identificador en el token o contexto
            var connectionId = Context.ConnectionId;

            if (!string.IsNullOrEmpty(userId))
            {
                _userConnections.AddOrUpdate(userId,
                    new List<string> { connectionId },
                    (key, existingConnections) =>
                    {
                        existingConnections.Add(connectionId);
                        return existingConnections;
                    });
            }

            Console.WriteLine($"Usuario {userId} conectado con ConnectionId {connectionId}");
            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception exception)
        {
            //var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            //if (!string.IsNullOrEmpty(userId))
            //{
            //    await Groups.RemoveFromGroupAsync(Context.ConnectionId, userId);
            //}
            //await base.OnDisconnectedAsync(exception);
            var userId = Context.UserIdentifier;
            var connectionId = Context.ConnectionId;

            if (!string.IsNullOrEmpty(userId) && _userConnections.ContainsKey(userId))
            {
                _userConnections[userId].Remove(connectionId);

                if (!_userConnections[userId].Any())
                {
                    _userConnections.TryRemove(userId, out _);
                }
            }

            Console.WriteLine($"Usuario {userId} desconectado con ConnectionId {connectionId}");
            await base.OnDisconnectedAsync(exception);
        }

        public List<string> GetConnections(string userId)
        {
            return _userConnections.ContainsKey(userId) ? _userConnections[userId] : new List<string>();
        }

        public async Task NotifyUserAsync(string userId, string message)
        {
            if (_userConnections.TryGetValue(userId, out var connectionIds))
            {
                foreach (var connectionId in connectionIds)
                {
                    await Clients.Client(connectionId).SendAsync("ReceiveMessage", message);
                }
            }
        }

        public string GetConnectionId()
        {
            return Context.ConnectionId;
        }




        public async Task PingRequestAsync(string userId)
        {
            await Clients.All.SendAsync(ApplicationConstants.SignalR.PingRequest, userId);
        }
        public async Task PingResponseAsync(string userId, string requestedUserId)
        {
            await Clients.User(requestedUserId).SendAsync(ApplicationConstants.SignalR.PingResponse, userId);
        }
        public async Task OnConnectAsync(string userId)
        {
            await Clients.All.SendAsync(ApplicationConstants.SignalR.ConnectUser, userId);
        }

        public async Task OnDisconnectAsync(string userId)
        {
            await Clients.All.SendAsync(ApplicationConstants.SignalR.DisconnectUser, userId);
        }

        public async Task OnChangeRolePermissions(string userId, string roleId)
        {
            await Clients.All.SendAsync(ApplicationConstants.SignalR.LogoutUsersByRole, userId, roleId);
        }

        public async Task SendMessageAsync( string userName)
        {
            await Clients.All.SendAsync(ApplicationConstants.SignalR.ReceiveMessage, userName);
          //  await Clients.User(chatHistory.FromUserId).SendAsync(ApplicationConstants.SignalR.ReceiveMessage, chatHistory, userName);
        }
        public async Task ChatNotificationAsync(string message, string receiverUserId, string senderUserId)
        {
            await Clients.User(receiverUserId).SendAsync(ApplicationConstants.SignalR.ReceiveChatNotification, message, receiverUserId, senderUserId);
        }

        public async Task UpdateDashboardAsync()
        {
            await Clients.All.SendAsync(ApplicationConstants.SignalR.ReceiveUpdateDashboard);
        }

        public async Task RegenerateTokensAsync()
        {
            await Clients.All.SendAsync(ApplicationConstants.SignalR.ReceiveRegenerateTokens);
        }


     
        /// <summary>Sin refresh token y con nuevo security stamp: sus JWT abiertos dejan de valer en el siguiente request.</summary>
        private async Task RevokeSessionsAsync(AdmUser user)
        {
            user.RefreshToken = null;
            user.RefreshTokenExpiryTime = _dateTimeService.NowUtc;
            await _userManager.UpdateAsync(user);
            await _userManager.UpdateSecurityStampAsync(user);
            _sessions.Invalidate(user.Id);
        }

        [Authorize(Policy = Permissions.Users.ChangeState)]
        public async Task ForceLogoutByRoleAsync(string roleId)
        {
            var role = await _roleService.GetByIdAsync(roleId);
            // Buscar todos los usuarios con el rol especificado
            var usersInRole = await _userManager.GetUsersInRoleAsync(role.Data.Name);
            if (usersInRole != null)
            {
                foreach (var user in usersInRole)
                {
                    try
                    {
                        await RevokeSessionsAsync(user);

                        // Log additional information
                        Console.WriteLine($"Attempting to send ForceLogout to user: {user.Id}");

                        await Clients.User(user.Id).SendAsync("ForceLogout", "Sesión cerrada por el administrador");

                        Console.WriteLine($"ForceLogout sent successfully to user: {user.Id}");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"{ex}, Error sending ForceLogout to user {user.Id}: {ex.Message}");
                    }
                }
            }

            // Notificar a todos los administradores sobre la acción
            await Clients.All.SendAsync("LogoutNotification", $"Usuarios del rol {role.Data.Name} han sido desconectados");
        }


        // Método para cerrar sesión de un usuario específico
        [Authorize(Policy = Permissions.Users.ChangeState)]
        public async Task ForceLogoutUserAsync(string userId)
        {
            
                var user = await _userManager.FindByIdAsync(userId);
            if (user != null)
            {
                try
                {
                    // Invalida el refresh token y los JWT del usuario
                    await RevokeSessionsAsync(user);

                    // Notifica al usuario sobre la sesión cerrada
                    await Clients.User(userId).SendAsync("ForceLogout", "Tu sesión ha sido cerrada por el administrador.");
                    Console.WriteLine($"ForceLogout enviado con éxito al usuario: {userId}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error al enviar ForceLogout al usuario {userId}: {ex.Message}");
                }
            }
        }

        // Método para cerrar sesión de usuarios específicos
        [Authorize(Policy = Permissions.Users.ChangeState)]
        public async Task ForceLogoutUsersAsync(List<string> userIds)
        {
            foreach (var userId in userIds)
            {
                var user = await _userManager.FindByIdAsync(userId);
                if (user != null)
                {
                    // Invalidar refresh token y JWT
                    await RevokeSessionsAsync(user);

                    // Notificar al cliente para forzar logout
                    await Clients.User(user.Id).SendAsync("ForceLogoutUsers", "Sesión cerrada por administrador");
                }
            }

            // Notificar a todos los administradores sobre la acción
            await Clients.All.SendAsync("LogoutNotificationUser", $"Usuarios seleccionados han sido desconectados");
        }
    }
}