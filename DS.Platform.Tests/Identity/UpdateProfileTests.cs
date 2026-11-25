using DS.Platform.Application.DTOs.Identities.Request;
using DS.Platform.Application.Validators.Features.Files;
using DS.Platform.Domain.Entities.Identity;
using DS.Platform.Infrastructure.Services.Identity;
using DS.Platform.Shared.Constants.Role;
using DS.Platform.Shared.Static;
using DS.Platform.Tests.Support;

namespace DS.Platform.Tests.Identity
{
    /// <summary>
    /// Editar un usuario desde la administración (PUT api/identity/account/UpdateProfile, JSON) y subir su foto aparte
    /// (POST profile-picture/{userId}, multipart): se guardan nombres, correo y teléfono. En la consola (sin empresas) un
    /// administrador llega a todos los usuarios menos a los SuperAdmin, que solo administra otro SuperAdmin.
    /// </summary>
    public class UpdateProfileTests
    {
        private readonly IdentityFactory _identity = new();

        private async Task<(string ana, string root)> SeedAsync()
        {
            var ana = await _identity.CreateUserAsync("ana");
            var root = await _identity.CreateUserAsync("root");
            await using var scope = _identity.Open();
            if (!await scope.Roles.RoleExistsAsync(RoleConstants.SuperAdminRole))
            {
                await scope.Roles.CreateAsync(new AdmRole(RoleConstants.SuperAdminRole) { IsActive = true, CreatedBy = "tests" });
            }
            await scope.Users.AddToRoleAsync((await scope.Users.FindByIdAsync(root))!, RoleConstants.SuperAdminRole);
            return (ana, root);
        }

        private static TestCurrentUserService Admin() => new() { UserId = "admin" };

        private static AccountService Account(IdentityScope scope, TestCurrentUserService user, FakeUploadService upload) => new(
            scope.Users, upload, new FileUploadValid(new TestLocalizer()), new TestLocalizer(), user, scope.Sessions,
            HandlerKit.UnitOfWork<int>(scope.Db, user), new DS.Platform.Infrastructure.Services.Identity.TrustedDeviceService(scope.Db));

        [Fact]
        public async Task The_admin_saves_names_email_and_phone_of_a_user()
        {
            var (ana, _) = await SeedAsync();
            var admin = Admin();

            await using (var scope = _identity.Open(admin))
            {
                var result = await Account(scope, admin, new FakeUploadService()).UpdateProfileAsync(new UpdateProfileRequest
                {
                    ProfileId = ana, FirstName = "ANA MARÍA", LastName = "RUIZ", Email = "ana.ruiz@tests.local", PhoneNumber = "966464645",
                    UserLogin = "ana"
                });
                Assert.True(result.Succeeded, string.Join(" ", result.Messages));
            }

            await using (var scope = _identity.Open())
            {
                var saved = await scope.Users.FindByIdAsync(ana);
                Assert.Equal(("ANA MARÍA", "RUIZ", "966464645"), (saved!.FirstName, saved.LastName, saved.PhoneNumber));
                // El correo se guarda, sigue confirmado y se puede buscar (normalizado).
                Assert.Equal("ana.ruiz@tests.local", saved.Email);
                Assert.True(saved.EmailConfirmed);
                Assert.Equal(ana, (await scope.Users.FindByEmailAsync("ANA.RUIZ@tests.local"))?.Id);
            }
        }

        [Fact]
        public async Task A_non_platform_admin_cannot_edit_a_SuperAdmin()
        {
            var (_, root) = await SeedAsync();
            var admin = Admin();

            await using var scope = _identity.Open(admin);
            var result = await Account(scope, admin, new FakeUploadService()).UpdateProfileAsync(new UpdateProfileRequest
            {
                ProfileId = root, FirstName = "X", LastName = "Y", Email = "root@tests.local", UserLogin = "root"
            });

            Assert.False(result.Succeeded);
            Assert.Contains(UserAdminScope.DeniedMessage, result.Messages);
        }

        [Fact]
        public async Task The_profile_picture_is_uploaded_apart_only_for_a_reachable_user_and_only_an_image()
        {
            var (ana, root) = await SeedAsync();
            var admin = Admin();
            var upload = new FakeUploadService();

            await using var scope = _identity.Open(admin);
            var account = Account(scope, admin, upload);

            // Un SuperAdmin (fuera del alcance de este administrador): rechazado sin tocar el almacenamiento.
            Assert.False((await account.UpdateProfilePictureAsync(FakeUploadService.File("foto.png", FakeUploadService.Png), root)).Succeeded);
            // Un archivo que no es imagen: rechazado.
            Assert.False((await account.UpdateProfilePictureAsync(FakeUploadService.File("foto.png", "texto"u8.ToArray()), ana)).Succeeded);
            Assert.Empty(upload.Calls);

            // La primera foto se guarda; la segunda reemplaza a la anterior.
            var first = await account.UpdateProfilePictureAsync(FakeUploadService.File("foto.png", FakeUploadService.Png), ana);
            Assert.True(first.Succeeded, string.Join(" ", first.Messages));
            var second = await account.UpdateProfilePictureAsync(FakeUploadService.File("foto2.png", FakeUploadService.Png), ana);
            Assert.True(second.Succeeded);
            Assert.Equal([$"save {FileFolderContainers.USERS}", $"edit {FileFolderContainers.USERS} {first.Data}"], upload.Calls);
            Assert.Equal(second.Data, (await scope.Users.FindByIdAsync(ana))!.ProfilePictureDataUrl);
        }
    }
}
