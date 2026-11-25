using DS.Platform.Application.DTOs.Identities.Response;
using DS.Platform.Application.Interfaces.Services;
using DS.Platform.Domain.Entities.Identity;
using DS.Platform.Infrastructure.Helpers;
using DS.Platform.Infrastructure.Persistence.Contexts;
using DS.Platform.Shared.Constants.Permission;
using DS.Platform.Shared.Constants.Role;
using DS.Platform.Shared.Constants.User;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DS.Platform.Infrastructure.Persistence.Seed
{
    /// <summary>
    /// Seeder inicial de seguridad (idempotente, corre en cada arranque):
    /// <list type="bullet">
    /// <item>Crea los roles de <see cref="SystemRoleCatalog"/> que falten, con sus permisos por defecto <b>solo al crearlos</b>
    /// (después mandan los cambios hechos en Roles → Permisos). SuperAdmin recibe en cada arranque todo permiso nuevo.</item>
    /// <item>Usuario <c>superadmin</c> con el rol SuperAdmin (si venía con Administrator, se le cambia).</item>
    /// <item>Usuario <c>administrador</c> con el rol Administrator (configurable), en la primera sucursal activa si hay.</item>
    /// <item>Usuario <c>basicadmin</c> con el rol Básico, solo en Development.</item>
    /// </list>
    /// La contraseña inicial sale de <c>Seed:InitialAdminPassword</c> (user-secrets o variable de entorno
    /// <c>Seed__InitialAdminPassword</c>); solo en Development, si falta, se usa <see cref="UserConstants.DefaultPassword"/>.
    /// Fuera de Development sin esa clave no se crea ningún usuario (una contraseña escrita en el código la conoce cualquiera
    /// que lea el repositorio, y quien entrara primero enrolaría su propio MFA). El primer ingreso exige enrolar el MFA.
    /// </summary>
    public class DatabaseSeeder : IDatabaseSeeder
    {
        public const string SuperAdminUserName = "superadmin";
        public const string AdministratorUserName = "administrador";
        public const string BasicUserName = "basicadmin";

        private const string SeedUser = "System";

        /// <summary>Clave de configuración con la contraseña inicial de los usuarios del seeder.</summary>
        public const string InitialPasswordKey = "Seed:InitialAdminPassword";

        private readonly ILogger<DatabaseSeeder> _logger;
        private readonly ApplicationDbContext _db;
        private readonly UserManager<AdmUser> _userManager;
        private readonly RoleManager<AdmRole> _roleManager;
        private readonly IConfiguration _configuration;
        private readonly IHostEnvironment _environment;
        private string? _initialPassword;

        public DatabaseSeeder(
            UserManager<AdmUser> userManager,
            RoleManager<AdmRole> roleManager,
            ApplicationDbContext db,
            ILogger<DatabaseSeeder> logger,
            IConfiguration configuration,
            IHostEnvironment environment
            )
        {
            _configuration = configuration;
            _environment = environment;
            _userManager = userManager;
            _roleManager = roleManager;
            _db = db;
            _logger = logger;
        }

        public void Initialize()
        {
            Task.Run(SeedAsync).GetAwaiter().GetResult();
        }

        public async Task SeedAsync()
        {
            var catalog = new List<RoleClaimResponse>();
            catalog.GetAllPermissions();

            foreach (var definition in SystemRoleCatalog.Roles)
            {
                await SeedRoleAsync(definition, catalog);
            }

            _initialPassword = InitialPassword();
            if (_initialPassword is null)
            {
                _logger.LogWarning("No se crean los usuarios iniciales: falta la contraseña inicial ({Key}).", InitialPasswordKey);
            }
            await SeedSuperAdminUserAsync();
            await SeedAdministratorUserAsync();
            if (_environment.IsDevelopment())
            {
                await SeedBasicUserAsync();
            }
            EnableLockoutForExistingUsers();
            await _db.SaveChangesAsync();
        }

        /// <summary>Crea el rol si falta (con sus permisos por defecto) y mantiene su descripción; SuperAdmin siempre con todo.</summary>
        private async Task SeedRoleAsync(SystemRoleDefinition definition, List<RoleClaimResponse> catalog)
        {
            var role = await _roleManager.FindByNameAsync(definition.Name);
            if (role is null && definition.LegacyName is not null)
            {
                // Rol creado con el nombre anterior en inglés: se renombra y conserva Id, permisos, usuarios y menús.
                role = await _roleManager.FindByNameAsync(definition.LegacyName);
                if (role is not null)
                {
                    await _roleManager.SetRoleNameAsync(role, definition.Name);
                    role.LastModifiedBy = SeedUser;
                    role.LastModifiedOn = DateTime.UtcNow;
                    await _roleManager.UpdateAsync(role);
                    _logger.LogInformation("Rol {Legacy} renombrado a {Role}.", definition.LegacyName, definition.Name);
                }
            }

            var created = false;
            if (role is null)
            {
                var result = await _roleManager.CreateAsync(new AdmRole(definition.Name, definition.Description)
                {
                    IsActive = true,
                    CreatedBy = SeedUser,
                    CreatedOn = DateTime.UtcNow
                });
                if (!result.Succeeded)
                {
                    _logger.LogError("No se pudo crear el rol {Role}: {Errors}", definition.Name, string.Join(" ", result.Errors.Select(e => e.Description)));
                    return;
                }
                role = await _roleManager.FindByNameAsync(definition.Name);
                created = true;
                _logger.LogInformation("Rol {Role} creado.", definition.Name);
            }
            else if (role.Description != definition.Description)
            {
                role.Description = definition.Description;
                role.LastModifiedBy = SeedUser;
                role.LastModifiedOn = DateTime.UtcNow;
                await _roleManager.UpdateAsync(role);
            }

            var wanted = definition.Grant switch
            {
                SystemRoleGrant.AllAlways => catalog.Select(c => c.Value!).ToList(),
                SystemRoleGrant.AllOnCreate when created => catalog.Select(c => c.Value!).ToList(),
                SystemRoleGrant.ReadOnly when created => catalog.Select(c => c.Value!).Where(SystemRoleCatalog.IsReadOnly).ToList(),
                SystemRoleGrant.Listed when created => definition.Permissions.ToList(),
                _ => []
            };
            if (wanted.Count == 0)
            {
                return;
            }

            var existing = (await _roleManager.GetClaimsAsync(role!))
                .Where(c => c.Type == ApplicationClaimTypes.Permission)
                .Select(c => c.Value)
                .ToHashSet(StringComparer.Ordinal);
            var byValue = catalog.GroupBy(c => c.Value!).ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);
            var added = 0;
            foreach (var permission in wanted.Distinct(StringComparer.Ordinal))
            {
                if (existing.Contains(permission))
                {
                    continue;
                }
                byValue.TryGetValue(permission, out var info);
                _db.Set<AdmRoleClaim>().Add(new AdmRoleClaim
                {
                    RoleId = role!.Id,
                    ClaimType = ApplicationClaimTypes.Permission,
                    ClaimValue = permission,
                    Description = info?.Description ?? permission,
                    Group = info?.Group ?? string.Empty,
                    CreatedBy = SeedUser,
                    CreatedOn = DateTime.UtcNow,
                    IsActive = true
                });
                added++;
            }
            if (added > 0)
            {
                await _db.SaveChangesAsync();
                _logger.LogInformation("Rol {Role}: {Count} permisos agregados.", definition.Name, added);
            }
        }

        /// <summary>superadmin siempre con SuperAdmin; si venía con Administrator (antes era el rol con todo), se le quita.</summary>
        private async Task SeedSuperAdminUserAsync()
        {
            var user = await _userManager.FindByNameAsync(SuperAdminUserName)
                ?? await _userManager.FindByEmailAsync(LegacySuperAdminEmail);
            if (user is null)
            {
                user = NewUser(SuperAdminUserName, SuperAdminEmail, "Super", "Admin");
                await CreateAsync(user, RoleConstants.SuperAdminRole);
                return;
            }
            await ReplaceLegacyEmailAsync(user, LegacySuperAdminEmail, SuperAdminEmail);

            if (await _userManager.IsInRoleAsync(user, RoleConstants.SuperAdminRole))
            {
                return;
            }

            await _userManager.AddToRoleAsync(user, RoleConstants.SuperAdminRole);
            if (await _userManager.IsInRoleAsync(user, RoleConstants.AdministratorRole))
            {
                await _userManager.RemoveFromRoleAsync(user, RoleConstants.AdministratorRole);
            }
            // Sus JWT abiertos llevan los roles anteriores: el nuevo stamp los rechaza y el frontend renueva con los nuevos.
            await _userManager.UpdateSecurityStampAsync(user);
            _logger.LogInformation("Usuario {User} pasado al rol {Role}.", SuperAdminUserName, RoleConstants.SuperAdminRole);
        }

        /// <summary>Administrador de la consola (permisos configurables desde Roles).</summary>
        private async Task SeedAdministratorUserAsync()
        {
            if (await _userManager.FindByNameAsync(AdministratorUserName) is not null)
            {
                return;
            }

            var user = NewUser(AdministratorUserName, "administrador@consola.local", "Administrador", "Consola");
            await CreateAsync(user, RoleConstants.AdministratorRole);
        }

        private async Task SeedBasicUserAsync()
        {
            var existing = await _userManager.FindByNameAsync(BasicUserName) ?? await _userManager.FindByEmailAsync(LegacyBasicEmail);
            if (existing is not null)
            {
                await ReplaceLegacyEmailAsync(existing, LegacyBasicEmail, BasicEmail);
                return;
            }
            await CreateAsync(NewUser(BasicUserName, BasicEmail, "Basic", "Admin"), RoleConstants.BasicRole);
        }

        /// <summary>
        /// Correos de los usuarios sembrados: dominio <c>.local</c>, que no se entrega. Antes eran buzones de Gmail de terceros
        /// marcados como confirmados: "olvidé mi contraseña" enviaba allí el enlace del SuperAdmin (auditoría run-1).
        /// </summary>
        public const string SuperAdminEmail = "superadmin@consola.local";
        public const string BasicEmail = "basic@consola.local";
        private const string LegacySuperAdminEmail = "superadmin@gmail.com";
        private const string LegacyBasicEmail = "basic@gmail.com";

        /// <summary>
        /// Si el usuario sigue con el correo de ejemplo antiguo, lo cambia por el <c>.local</c> (queda sin confirmar: no hay canal
        /// de recuperación hasta que un administrador ponga un correo real). Un correo ya cambiado no se toca.
        /// </summary>
        private async Task ReplaceLegacyEmailAsync(AdmUser user, string legacy, string replacement)
        {
            if (!string.Equals(user.Email, legacy, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }
            var result = await _userManager.SetEmailAsync(user, replacement);
            if (result.Succeeded)
            {
                _logger.LogWarning("Usuario {User}: el correo de ejemplo de un tercero se cambió por {Email}. Registre un correo real.", user.UserName, replacement);
            }
        }

        private static AdmUser NewUser(string userName, string email, string firstName, string lastName) => new()
        {
            FirstName = firstName,
            LastName = lastName,
            Email = email,
            UserName = userName,
            EmailConfirmed = true,
            PhoneNumberConfirmed = true,
            CreatedOn = DateTime.UtcNow,
            CreatedBy = SeedUser,
            IsActive = true
        };

        private string? InitialPassword()
        {
            var configured = _configuration[InitialPasswordKey];
            if (!string.IsNullOrWhiteSpace(configured))
            {
                return configured;
            }
            return _environment.IsDevelopment() ? UserConstants.DefaultPassword : null;
        }

        private async Task CreateAsync(AdmUser user, string role)
        {
            if (_initialPassword is null)
            {
                return;
            }
            var created = await _userManager.CreateAsync(user, _initialPassword);
            if (!created.Succeeded)
            {
                _logger.LogError("No se pudo crear el usuario {User}: {Errors}", user.UserName, string.Join(" ", created.Errors.Select(e => e.Description)));
                return;
            }
            var added = await _userManager.AddToRoleAsync(user, role);
            if (!added.Succeeded)
            {
                _logger.LogError("No se pudo asignar el rol {Role} a {User}: {Errors}", role, user.UserName, string.Join(" ", added.Errors.Select(e => e.Description)));
                return;
            }
            _logger.LogInformation("Usuario {User} creado con el rol {Role}.", user.UserName, role);
        }

        /// <summary>
        /// Fase 12a: el bloqueo por intentos fallidos aplica a todos. Los usuarios creados antes tenían LockoutEnabled = false
        /// (los nuevos lo traen por AllowedForNewUsers). Actualización de datos idempotente, sin migración.
        /// </summary>
        private void EnableLockoutForExistingUsers()
        {
            if (!_db.Database.IsRelational())
            {
                // Base en memoria (tests): sin ExecuteUpdate; lo guarda el SaveChanges de SeedAsync.
                foreach (var user in _db.Users.Where(u => !u.LockoutEnabled))
                {
                    user.LockoutEnabled = true;
                }
                return;
            }

            var updated = _db.Users.Where(u => !u.LockoutEnabled)
                .ExecuteUpdate(setters => setters.SetProperty(u => u.LockoutEnabled, true));
            if (updated > 0)
            {
                _logger.LogInformation("Bloqueo por intentos habilitado para {Count} usuarios existentes.", updated);
            }
        }
    }
}
