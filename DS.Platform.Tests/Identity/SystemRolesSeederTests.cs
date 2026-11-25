using DS.Platform.Application.DTOs.Identities.Response;
using DS.Platform.Domain.Entities.Identity;
using DS.Platform.Infrastructure.Helpers;
using DS.Platform.Infrastructure.Persistence.Seed;
using DS.Platform.Shared.Constants.Permission;
using DS.Platform.Shared.Constants.Role;
using DS.Platform.Tests.Support;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;

namespace DS.Platform.Tests.Identity
{
    /// <summary>
    /// Seeder inicial de roles y usuarios: todos los roles de <see cref="SystemRoleCatalog"/>, superadmin con SuperAdmin (todo),
    /// administrador con Administrador (configurable), permisos por defecto solo al crear el rol.
    /// </summary>
    public class SystemRolesSeederTests
    {
        private readonly IdentityFactory _identity = new();

        private static List<string> Catalog()
        {
            var all = new List<RoleClaimResponse>();
            all.GetAllPermissions();
            return all.Select(p => p.Value!).Distinct().ToList();
        }

        private Task SeedAsync() => SeedAsync(Environments.Development);

        private async Task SeedAsync(string environment, string? initialPassword = null)
        {
            await using var scope = _identity.Open();
            var settings = new Dictionary<string, string?>();
            if (initialPassword is not null)
            {
                settings[DatabaseSeeder.InitialPasswordKey] = initialPassword;
            }
            var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
            await new DatabaseSeeder(scope.Users, scope.Roles, scope.Db, NullLogger<DatabaseSeeder>.Instance, configuration, new TestHostEnvironment(environment)).SeedAsync();
        }

        private sealed class TestHostEnvironment(string name) : IHostEnvironment
        {
            public string EnvironmentName { get; set; } = name;
            public string ApplicationName { get; set; } = "DS.Platform.Tests";
            public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
            public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
        }

        [Fact]
        public async Task Outside_development_users_are_created_only_with_the_configured_initial_password()
        {
            // Sin la clave: roles sí, usuarios no (la contraseña del código no sirve en un servidor real).
            await SeedAsync(Environments.Production);
            await using (var scope = _identity.Open())
            {
                Assert.NotNull(await scope.Roles.FindByNameAsync(RoleConstants.SuperAdminRole));
                Assert.Null(await scope.Users.FindByNameAsync(DatabaseSeeder.SuperAdminUserName));
            }

            const string secret = "Piloto-2026!x";
            await SeedAsync(Environments.Production, secret);
            await using (var scope = _identity.Open())
            {
                var super = await scope.Users.FindByNameAsync(DatabaseSeeder.SuperAdminUserName);
                Assert.NotNull(super);
                Assert.True(await scope.Users.CheckPasswordAsync(super!, secret));
                Assert.False(await scope.Users.CheckPasswordAsync(super!, IdentityFactory.Password));
                // basicadmin es solo para desarrollo.
                Assert.Null(await scope.Users.FindByNameAsync(DatabaseSeeder.BasicUserName));
            }
        }

        private async Task<List<string>> PermissionsOfAsync(string roleName)
        {
            await using var scope = _identity.Open();
            var role = await scope.Roles.FindByNameAsync(roleName);
            Assert.NotNull(role);
            return (await scope.Roles.GetClaimsAsync(role!))
                .Where(c => c.Type == ApplicationClaimTypes.Permission)
                .Select(c => c.Value)
                .OrderBy(v => v)
                .ToList();
        }

        private async Task<IList<string>> RolesOfAsync(string userName)
        {
            await using var scope = _identity.Open();
            var user = await scope.Users.FindByNameAsync(userName);
            Assert.NotNull(user);
            return await scope.Users.GetRolesAsync(user!);
        }

        [Fact]
        public async Task Fresh_seed_creates_every_system_role_and_the_two_admin_users()
        {
            await SeedAsync();

            await using (var scope = _identity.Open())
            {
                foreach (var definition in SystemRoleCatalog.Roles)
                {
                    var role = await scope.Roles.FindByNameAsync(definition.Name);
                    Assert.NotNull(role);
                    Assert.Equal(definition.Description, role!.Description);
                }
            }

            Assert.Equal([RoleConstants.SuperAdminRole], await RolesOfAsync(DatabaseSeeder.SuperAdminUserName));
            Assert.Equal([RoleConstants.AdministratorRole], await RolesOfAsync(DatabaseSeeder.AdministratorUserName));
            Assert.Equal([RoleConstants.BasicRole], await RolesOfAsync(DatabaseSeeder.BasicUserName));
        }

        [Fact]
        public async Task Each_role_gets_its_default_permissions()
        {
            await SeedAsync();
            var catalog = Catalog().OrderBy(v => v).ToList();

            Assert.Equal(catalog, await PermissionsOfAsync(RoleConstants.SuperAdminRole));
            Assert.Equal(catalog, await PermissionsOfAsync(RoleConstants.AdministratorRole));
            Assert.Equal(SystemRoleCatalog.Find(RoleConstants.BillingRole)!.Permissions.Distinct().OrderBy(v => v),
                await PermissionsOfAsync(RoleConstants.BillingRole));
            Assert.Equal([Permissions.Dashboards.Menu, Permissions.Dashboards.View, Permissions.Preferences.ChangeLanguage],
                await PermissionsOfAsync(RoleConstants.BasicRole));

            var readOnly = await PermissionsOfAsync(RoleConstants.ReadOnlyRole);
            Assert.NotEmpty(readOnly);
            Assert.All(readOnly, p => Assert.True(SystemRoleCatalog.IsReadOnly(p), p));
            Assert.Contains(Permissions.Plans.View, readOnly);
            Assert.DoesNotContain(Permissions.Plans.Create, readOnly);
            Assert.DoesNotContain(Permissions.Users.Edit, readOnly);
        }

        [Fact]
        public async Task Running_twice_changes_nothing()
        {
            await SeedAsync();
            int claims, users;
            await using (var scope = _identity.Open())
            {
                claims = scope.Db.Set<AdmRoleClaim>().Count();
                users = scope.Db.Users.Count();
            }

            await SeedAsync();

            await using var after = _identity.Open();
            Assert.Equal(claims, after.Db.Set<AdmRoleClaim>().Count());
            Assert.Equal(users, after.Db.Users.Count());
            Assert.Equal(SystemRoleCatalog.Roles.Count, after.Db.Roles.Count());
        }

        [Fact]
        public async Task Permissions_removed_from_a_role_in_the_UI_are_not_restored_but_SuperAdmin_always_has_all()
        {
            await SeedAsync();
            await using (var scope = _identity.Open())
            {
                foreach (var roleName in new[] { RoleConstants.AdministratorRole, RoleConstants.BillingRole, RoleConstants.SuperAdminRole })
                {
                    var role = await scope.Roles.FindByNameAsync(roleName);
                    var claim = (await scope.Roles.GetClaimsAsync(role!)).First(c => c.Value == Permissions.Plans.View);
                    Assert.True((await scope.Roles.RemoveClaimAsync(role!, claim)).Succeeded);
                }
            }

            await SeedAsync();

            Assert.DoesNotContain(Permissions.Plans.View, await PermissionsOfAsync(RoleConstants.AdministratorRole));
            Assert.DoesNotContain(Permissions.Plans.View, await PermissionsOfAsync(RoleConstants.BillingRole));
            Assert.Contains(Permissions.Plans.View, await PermissionsOfAsync(RoleConstants.SuperAdminRole));
        }

        [Fact]
        public void Every_listed_permission_exists_and_read_only_never_includes_writes()
        {
            var catalog = Catalog().ToHashSet();
            foreach (var definition in SystemRoleCatalog.Roles)
            {
                Assert.All(definition.Permissions, p => Assert.Contains(p, catalog));
            }

            Assert.True(SystemRoleCatalog.IsSystemRole(RoleConstants.BillingRole));
            Assert.False(SystemRoleCatalog.IsSystemRole("Bodega"));
            Assert.True(SystemRoleCatalog.IsReservedName("superadmin"));
            Assert.True(SystemRoleCatalog.IsReservedName("platformadmin"));
            foreach (var write in new[] { Permissions.Plans.Create, Permissions.Plans.Edit, Permissions.Plans.Delete,
                                          Permissions.Plans.ChangeState, Permissions.Users.ResetMfa, Permissions.Roles.Edit })
            {
                Assert.False(SystemRoleCatalog.IsReadOnly(write), write);
            }
        }
    }
}
