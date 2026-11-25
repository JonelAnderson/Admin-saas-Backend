using DS.Platform.Application.DTOs.Identities.Request;
using DS.Platform.Application.DTOs.Menu.Response;
using DS.Platform.Application.Features.Menus.Queries.GetUserMenu;
using DS.Platform.Domain.Entities.Identity;
using DS.Platform.Domain.Entities.Menus;
using DS.Platform.Infrastructure.Persistence.Contexts;
using DS.Platform.Infrastructure.Services.Identity;
using DS.Platform.Infrastructure.Repositories;
using DS.Platform.Shared.Localization;
using DS.Platform.Tests.Support;
using LazyCache;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;

namespace DS.Platform.Tests.Menus
{
    /// <summary>
    /// Regressions: unchecked menus stayed assigned (soft-deleted MenuRole rows were still read) and
    /// deleted / inactive menus kept showing in the sidebar.
    /// </summary>
    public sealed class RoleMenuNavigationTests : IAsyncDisposable
    {
        private const string UserId = "user-1";

        private readonly ApplicationDbContext _db;
        private readonly ServiceProvider _services;
        private readonly TestCurrentUserService _currentUser = new() { UserId = UserId };
        private string _roleId = string.Empty;

        public RoleMenuNavigationTests()
        {
            _db = new TestDatabase().CreateContext(TestCurrentUserService.NoContext());
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSingleton(_db);
            services.AddIdentityCore<AdmUser>().AddRoles<AdmRole>().AddEntityFrameworkStores<ApplicationDbContext>();
            _services = services.BuildServiceProvider();
        }

        private UserManager<AdmUser> Users => _services.GetRequiredService<UserManager<AdmUser>>();
        private RoleManager<AdmRole> Roles => _services.GetRequiredService<RoleManager<AdmRole>>();
        private UnitOfWork<int> UnitOfWork => new(_db, _currentUser, new CachingService());

        /// <summary>
        /// Navigation (group)
        /// ├── Home (item) #2
        /// └── Sales (collapse) #3
        ///     ├── Invoices (item) #4
        ///     └── Returns (item) #5
        /// Admin (group) #6
        /// └── Users (item) #7
        /// </summary>
        private async Task SeedAsync()
        {
            _db.Menus.AddRange(
                Menu(1, "Navigation", "group", null, 1),
                Menu(2, "Home", "item", 1, 1),
                Menu(3, "Sales", "collapse", 1, 2),
                Menu(4, "Invoices", "item", 3, 1),
                Menu(5, "Returns", "item", 3, 2),
                Menu(6, "Admin", "group", null, 2),
                Menu(7, "Users", "item", 6, 1));
            await _db.SaveChangesAsync();

            var role = new AdmRole("Cashier");
            await Roles.CreateAsync(role);
            _roleId = role.Id;
            var user = new AdmUser { Id = UserId, UserName = "cashier", Email = "cashier@test.local" };
            await Users.CreateAsync(user);
            await Users.AddToRoleAsync(user, "Cashier");
        }

        private static Menu Menu(int id, string title, string type, int? parentId, int order) => new()
        {
            Id = id,
            Title = title,
            Type = type,
            MenuParentId = parentId,
            Order = order,
            Url = type == "item" ? $"/private/{title.ToLowerInvariant()}" : null
        };

        private RoleService RoleService() => new(
            Roles, Users, roleClaimService: null!, _currentUser, UnitOfWork, new KeyLocalizer(),
            new DS.Platform.Infrastructure.Services.Identity.UserPermissionCache(new Microsoft.Extensions.Caching.Memory.MemoryCache(new Microsoft.Extensions.Caching.Memory.MemoryCacheOptions())));

        private async Task AssignAsync(params int[] menuIds)
        {
            var result = await RoleService().UpdateRoleMenusAsync(new UpdateRoleMenusRequest { RoleId = _roleId, MenuIds = [.. menuIds] });
            Assert.True(result.Succeeded, string.Join(" | ", result.Messages));
        }

        private async Task<List<int>> AssignedAsync() => (await RoleService().GetMenuIdsByRoleIdAsync(_roleId)).Data.Order().ToList();

        private async Task<List<MenuNavDto>> NavAsync()
        {
            var handler = new GetUserMenuQueryHandler(UnitOfWork, Users, Roles, _currentUser);
            var result = await handler.Handle(new GetUserMenuQuery { UserId = UserId }, CancellationToken.None);
            Assert.True(result.Succeeded, string.Join(" | ", result.Messages));
            return result.Data;
        }

        private static IEnumerable<string> Flatten(IEnumerable<MenuNavDto> nodes, string prefix = "")
            => nodes.SelectMany(n => new[] { prefix + n.Title }.Concat(Flatten(n.Children ?? [], prefix + n.Title + "/")));

        [Fact]
        public async Task Unchecked_menus_are_really_removed_from_the_role()
        {
            await SeedAsync();
            await AssignAsync(2, 4, 5, 7);

            await AssignAsync(2, 4);

            Assert.Equal([2, 4], await AssignedAsync());
            Assert.Equal(2, await _db.Set<MenuRole>().CountAsync());
            Assert.DoesNotContain("Admin/Users", Flatten(await NavAsync()));
        }

        [Fact]
        public async Task Saving_the_same_selection_again_does_not_accumulate_rows()
        {
            await SeedAsync();

            for (var i = 0; i < 3; i++)
            {
                await AssignAsync(2, 4);
            }

            Assert.Equal(2, await _db.Set<MenuRole>().CountAsync());
        }

        [Fact]
        public async Task Legacy_soft_deleted_assignments_stay_ignored()
        {
            await SeedAsync();
            _db.Set<MenuRole>().Add(new MenuRole { RoleId = _roleId, MenuId = 7 });
            await _db.SaveChangesAsync();
            // SaveChanges stamps IsDeleted = false on insert; mark it deleted like the old code did.
            var legacy = await _db.Set<MenuRole>().SingleAsync();
            legacy.IsDeleted = true;
            await _db.SaveChangesAsync();

            Assert.Empty(await AssignedAsync());
            Assert.Empty(await NavAsync());

            // Existing dead rows are purged by migration Phase4i; new saves no longer create them.
            await AssignAsync(2);
            Assert.Equal([2], await AssignedAsync());
            Assert.Equal(1, await _db.Set<MenuRole>().CountAsync(x => !x.IsDeleted));
        }

        [Fact]
        public async Task Unchecking_does_not_leave_dead_rows()
        {
            await SeedAsync();
            await AssignAsync(2, 4, 5);
            await AssignAsync(2);
            await AssignAsync(2, 7);

            Assert.Equal([2, 7], await AssignedAsync());
            Assert.Equal(2, await _db.Set<MenuRole>().CountAsync());
        }

        [Fact]
        public async Task Parents_appear_automatically_when_an_item_is_assigned()
        {
            await SeedAsync();
            await AssignAsync(4);

            Assert.Equal(["Navigation", "Navigation/Sales", "Navigation/Sales/Invoices"], Flatten(await NavAsync()));
        }

        [Fact]
        public async Task Assigned_container_without_visible_children_is_not_shown()
        {
            await SeedAsync();
            await AssignAsync(6, 2);

            Assert.Equal(["Navigation", "Navigation/Home"], Flatten(await NavAsync()));
        }

        [Fact]
        public async Task Deleted_or_inactive_menus_disappear_with_their_branch()
        {
            await SeedAsync();
            await AssignAsync(2, 4, 5, 7);

            var returns = await _db.Menus.FindAsync(5);
            returns!.IsDeleted = true;
            var sales = await _db.Menus.FindAsync(6);
            sales!.IsActive = false;
            await _db.SaveChangesAsync();

            Assert.Equal(["Navigation", "Navigation/Home", "Navigation/Sales", "Navigation/Sales/Invoices"], Flatten(await NavAsync()));
        }

        [Fact]
        public async Task A_super_admin_sees_every_menu_without_assignments()
        {
            await SeedAsync();
            await Roles.CreateAsync(new AdmRole(DS.Platform.Shared.Constants.Role.RoleConstants.SuperAdminRole));
            var user = await Users.FindByIdAsync(UserId);
            await Users.RemoveFromRoleAsync(user!, "Cashier");
            await Users.AddToRoleAsync(user!, DS.Platform.Shared.Constants.Role.RoleConstants.SuperAdminRole);

            Assert.Empty(await AssignedAsync());
            Assert.Equal(
                ["Navigation", "Navigation/Home", "Navigation/Sales", "Navigation/Sales/Invoices", "Navigation/Sales/Returns", "Admin", "Admin/Users"],
                Flatten(await NavAsync()));
        }

        public async ValueTask DisposeAsync()
        {
            await _services.DisposeAsync();
            await _db.DisposeAsync();
        }

        private sealed class KeyLocalizer : IStringLocalizer<ServerCommonResources>
        {
            public LocalizedString this[string name] => new(name, name);
            public LocalizedString this[string name, params object[] arguments] => new(name, name);
            public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => [];
        }
    }
}
