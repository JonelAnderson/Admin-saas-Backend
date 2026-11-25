using DS.Platform.Application.Configurations;
using DS.Platform.Application.Interfaces.Repositories;
using DS.Platform.Application.Interfaces.Services;
using DS.Platform.Application.Interfaces.Services.Identity;
using DS.Platform.Domain.Entities.Identity;
using DS.Platform.Infrastructure.Persistence.Contexts;
using DS.Platform.Infrastructure.Repositories;
using DS.Platform.Infrastructure.Services.Identity;
using DS.Platform.Shared.Constants.Role;
using LazyCache;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System.Buffers.Binary;
using System.Security.Cryptography;

namespace DS.Platform.Tests.Support
{
    /// <summary>
    /// Identity real (UserManager + AdmUserStore + opciones de la Fase 12a) sobre <see cref="TestDatabase"/>, con un
    /// anillo de Data Protection efímero compartido por todos los "requests" (<see cref="Open"/>) de una prueba.
    /// </summary>
    public sealed class IdentityFactory
    {
        public const string JwtSecret = "unit-tests-jwt-secret-with-more-than-32-chars";
        public const string JwtIssuer = "https://tests.ds-erp.local/";
        public const string Password = RoleConstants.DefaultPassword;

        public TestDatabase Database { get; } = new();
        public IDataProtectionProvider DataProtection { get; } = new EphemeralDataProtectionProvider();

        /// <summary>Un request: su DbContext, su usuario actual y los servicios de Identity.</summary>
        public IdentityScope Open(TestCurrentUserService? currentUser = null)
            => new(this, currentUser ?? TestCurrentUserService.NoContext());

        public async Task<string> CreateUserAsync(string userName, bool admin = false)
        {
            await using var scope = Open();
            var user = new AdmUser
            {
                UserName = userName,
                Email = $"{userName}@tests.local",
                FirstName = userName,
                LastName = "Test",
                EmailConfirmed = true,
                IsActive = true,
                CreatedBy = "tests"
            };
            var result = await scope.Users.CreateAsync(user, Password);
            Assert.True(result.Succeeded, string.Join(" | ", result.Errors.Select(e => e.Description)));
            return user.Id;
        }
    }

    public sealed class IdentityScope : IAsyncDisposable
    {
        private readonly ServiceProvider _services;

        public IdentityScope(IdentityFactory factory, TestCurrentUserService currentUser)
        {
            Db = factory.Database.CreateContext(currentUser);
            Tickets = new MfaTicketProtector(factory.DataProtection);

            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSingleton(Db);
            services.AddSingleton(factory.DataProtection);
            services.AddSingleton<ICurrentUserService>(currentUser);
            services.AddIdentityCore<AdmUser>(ErpIdentitySetup.Configure)
                .AddRoles<AdmRole>()
                .AddEntityFrameworkStores<ApplicationDbContext>()
                .AddErpIdentityHardening()
                .AddTokenProvider<AuthenticatorTokenProvider<AdmUser>>(TokenOptions.DefaultAuthenticatorProvider)
                // Como AddDefaultTokenProviders de la WebApi: tokens de "olvidé mi contraseña".
                .AddTokenProvider<DataProtectorTokenProvider<AdmUser>>(TokenOptions.DefaultProvider);
            services.AddSingleton(Tickets);
            services.AddSingleton<IOptions<AppConfiguration>>(Options.Create(new AppConfiguration
            {
                Secret = IdentityFactory.JwtSecret,
                Issuer = IdentityFactory.JwtIssuer,
                Expires = "1"
            }));
            services.AddSingleton<IUnitOfWork<int>>(new UnitOfWork<int>(Db, currentUser, new CachingService()));
            services.AddMemoryCache();
            services.AddSingleton<IUserPermissionCache, UserPermissionCache>();
            services.AddSingleton<ISessionStampService, SessionStampService>();
            services.AddTransient<IMfaService, MfaService>();
            services.AddTransient<ITrustedDeviceService, TrustedDeviceService>();
            services.AddTransient<IdentityService>();
            _services = services.BuildServiceProvider();
        }

        public ApplicationDbContext Db { get; }
        public MfaTicketProtector Tickets { get; }
        public UserManager<AdmUser> Users => _services.GetRequiredService<UserManager<AdmUser>>();
        public RoleManager<AdmRole> Roles => _services.GetRequiredService<RoleManager<AdmRole>>();
        public IdentityService Tokens => _services.GetRequiredService<IdentityService>();
        public IMfaService Mfa => _services.GetRequiredService<IMfaService>();
        public ISessionStampService Sessions => _services.GetRequiredService<ISessionStampService>();
        public IUserStore<AdmUser> Store => _services.GetRequiredService<IUserStore<AdmUser>>();

        public async ValueTask DisposeAsync()
        {
            await _services.DisposeAsync();
            await Db.DisposeAsync();
        }
    }

    /// <summary>TOTP RFC 6238 (HMAC-SHA1, 6 dígitos, 30 s), el mismo cálculo de la app autenticadora.</summary>
    public static class Totp
    {
        public static long CurrentStep => DateTimeOffset.UtcNow.ToUnixTimeSeconds() / 30;

        public static string Code(string sharedKey, long? step = null)
        {
            var key = FromBase32(sharedKey.Replace(" ", string.Empty));
            Span<byte> counter = stackalloc byte[8];
            BinaryPrimitives.WriteInt64BigEndian(counter, step ?? CurrentStep);
            var hash = HMACSHA1.HashData(key, counter);
            var offset = hash[^1] & 0x0f;
            var binary = (hash[offset] & 0x7f) << 24 | hash[offset + 1] << 16 | hash[offset + 2] << 8 | hash[offset + 3];
            return (binary % 1_000_000).ToString("D6");
        }

        /// <summary>Un código de 6 dígitos que no vale en la ventana de ±2 pasos que acepta Identity.</summary>
        public static string WrongCode(string sharedKey)
        {
            var valid = Enumerable.Range(-3, 7).Select(i => Code(sharedKey, CurrentStep + i)).ToHashSet();
            var candidate = int.Parse(Code(sharedKey));
            string wrong;
            do
            {
                candidate = (candidate + 1) % 1_000_000;
                wrong = candidate.ToString("D6");
            } while (valid.Contains(wrong));
            return wrong;
        }

        private static byte[] FromBase32(string input)
        {
            const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
            var text = input.TrimEnd('=').ToUpperInvariant();
            var output = new List<byte>();
            int buffer = 0, bits = 0;
            foreach (var c in text)
            {
                buffer = (buffer << 5) | alphabet.IndexOf(c);
                bits += 5;
                if (bits >= 8)
                {
                    bits -= 8;
                    output.Add((byte)(buffer >> bits));
                    buffer &= (1 << bits) - 1;
                }
            }
            return output.ToArray();
        }
    }
}
