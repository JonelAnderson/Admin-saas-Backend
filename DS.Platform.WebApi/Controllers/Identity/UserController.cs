using DS.Platform.Application.DTOs.Identities.Request;
using DS.Platform.Application.Interfaces.Services.Identity;
using DS.Platform.Shared.Commons.Bases.Request;
using DS.Platform.Shared.Constants.Permission;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DS.Platform.WebApi.Controllers.Identity
{
   // [Authorize]
    [Route("api/identity/user")]
    [ApiController]
    public class UserController : ControllerBase
    {
        private readonly IUserService _userService;

        public UserController(IUserService userService)
        {
            _userService = userService;
        }

        /// <summary>
        /// Get Users Details
        /// </summary>
        /// <returns>Status 200 OK</returns>
        [Authorize(Policy = Permissions.Users.View)]
        [HttpGet]
        public async Task<IActionResult> GetAll([FromQuery] BaseFiltersRequest baseFiltersRequest)
        {
            var users = await _userService.GetAllAsync(baseFiltersRequest);
            return Ok(users);
        }


        /// <summary>
        /// Get userse select
        /// </summary>
        /// <returns>Status 200</returns>
        /// <summary>Display names for user ids shown in documents (cash sessions, approvals, created by). Max 100 ids.</summary>
        [Authorize]
        [HttpGet("Names")]
        public async Task<IActionResult> Names([FromQuery] List<string> ids)
        {
            var wanted = (ids ?? new List<string>()).Where(i => !string.IsNullOrWhiteSpace(i)).Distinct().Take(100).ToList();
            var users = wanted.Count == 0 ? new List<DS.Platform.Application.DTOs.Identities.Response.UserByIdResponse>() : await _userService.GetUsersByIdsAsync(wanted);
            return Ok(users.Select(u => new
            {
                id = u.Id,
                name = string.IsNullOrWhiteSpace($"{u.FirstName} {u.LastName}".Trim()) ? u.UserName : $"{u.FirstName} {u.LastName}".Trim()
            }));
        }

        [Authorize(Policy = Permissions.Users.View)]
        [HttpGet("GetUserSelect")]
        public async Task<IActionResult> GetUserSelectAll()
        {
            var users = await _userService.GetUserSelectAsync();
            return Ok(users);
        }

        /// <summary>
        /// Get User By Id
        /// </summary>
        /// <param name="id"></param>
        /// <returns>Status 200 OK</returns>
        [Authorize(Policy = Permissions.Users.View)]
        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(string id)
        {
            var user = await _userService.GetAsync(id);
            return Ok(user);
        }


        /// <summary>
        /// Get User Roles By Id
        /// </summary>
        /// <param name="id"></param>
        /// <returns>Status 200 OK</returns>
        [Authorize(Policy = Permissions.Users.View)]
        [HttpGet("roles/{id}")]
        public async Task<IActionResult> GetRolesAsync(string id)
        {
            var userRoles = await _userService.GetRolesAsync(id);
            return Ok(userRoles);
        }

        /// <summary>
        /// Update Roles for User
        /// </summary>
        /// <param name="request"></param>
        /// <returns>Status 200 OK</returns>
        [Authorize(Policy = Permissions.Users.Edit)]
        [HttpPut("roles/{id}")]
        public async Task<IActionResult> UpdateRolesAsync(UpdateUserRolesRequest request)
        {
            return Ok(await _userService.UpdateRolesAsync(request));
        }

        /// <summary>
        /// Register a User
        /// </summary>
        /// <param name="request"></param>
        /// <returns>Status 200 OK</returns>
        //[AllowAnonymous]
        [Authorize(Policy = Permissions.Users.Create)]
        [HttpPost]
        public async Task<IActionResult> RegisterAsync([FromBody] RegisterRequest request)
        {
            //var origin = Request.Headers["origin"];
           
                var response = await _userService.RegisterAsync(request);
                return Ok(response);
           
        }

        /// <summary>
        /// Confirm Email
        /// </summary>
        /// <param name="userId"></param>
        /// <param name="code"></param>
        /// <returns>Status 200 OK</returns>
        [HttpGet("confirm-email")]
        [AllowAnonymous]
        public async Task<IActionResult> ConfirmEmailAsync([FromQuery] string userId, [FromQuery] string code)
        {
            return Ok(await _userService.ConfirmEmailAsync(userId, code));
        }

        /// <summary>
        /// Toggle User Status (Activate and Deactivate)
        /// </summary>
        /// <param name="request"></param>
        /// <returns>Status 200 OK</returns>
        [Authorize(Policy = Permissions.Users.ChangeState)]
        [HttpPost("toggle-status")]
        public async Task<IActionResult> ToggleUserStatusAsync(ToggleUserStatusRequest request)
        {
            return Ok(await _userService.ToggleUserStatusAsync(request));
        }

        /// <summary>
        /// Forgot Password
        /// </summary>
        /// <param name="request"></param>
        /// <returns>Status 200 OK</returns>
        [HttpPost("forgot-password")]
        [AllowAnonymous]
        public async Task<IActionResult> ForgotPasswordAsync(ForgotPasswordRequest request)
        {
            // El enlace del correo se arma con la URL del frontend configurada (AppConfiguration:ApplicationUrl), nunca con el
            // header Origin del pedido: con él un atacante podía hacer que el enlace apuntara a su sitio.
            return Ok(await _userService.ForgotPasswordAsync(request));
        }

        /// <summary>
        /// Reset Password
        /// </summary>
        /// <param name="request"></param>
        /// <returns>Status 200 OK</returns>
        [HttpPost("reset-password")]
        [AllowAnonymous]
        public async Task<IActionResult> ResetPasswordAsync(ResetPasswordRequest request)
        {
            return Ok(await _userService.ResetPasswordAsync(request));
        }

      
    }
}