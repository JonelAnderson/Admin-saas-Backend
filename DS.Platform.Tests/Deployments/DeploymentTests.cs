using System.Security.Cryptography.X509Certificates;
using DS.Platform.Application.DTOs.Deployments.Request;
using DS.Platform.Application.Features.Deployments.Commands.Cancel;
using DS.Platform.Application.Features.Deployments.Commands.Dispatch;
using DS.Platform.Application.Features.Deployments.Commands.Provision;
using DS.Platform.Application.Features.Deployments.Commands.Retry;
using DS.Platform.Application.Features.Deployments.Commands.Upgrade;
using DS.Platform.Application.Features.Deployments.Common;
using DS.Platform.Application.Features.Instances.Commands.Delete;
using DS.Platform.Application.Features.Instances.Common;
using DS.Platform.Application.Interfaces.Repositories;
using DS.Platform.Application.Interfaces.Services.Coolify;
using DS.Platform.Application.Interfaces.Services.Licensing;
using DS.Platform.Application.Interfaces.Services.Security;
using DS.Platform.Domain.Entities.Billing;
using DS.Platform.Domain.Entities.Customers;
using DS.Platform.Domain.Entities.Erp;
using DS.Platform.Domain.Entities.Ops;
using DS.Platform.Domain.Enums;
using DS.Platform.Infrastructure.Shared.Services.Coolify;
using DS.Platform.Shared.Wrapper;
using DS.Platform.Tests.Customers;
using DS.Platform.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;

namespace DS.Platform.Tests.Deployments
{
    /// <summary>Coolify de mentira: guarda lo que se le pide, falla a pedido (en un paso o en una variable) y cuenta las llamadas.</summary>
    public sealed class FakeCoolifyClient : ICoolifyClient
    {
        public List<CoolifyServiceSpec> Created { get; } = new();
        public List<(string Uuid, string Key, string Value)> Envs { get; } = new();
        public List<string> Started { get; } = new();
        public List<(string Uuid, bool Latest)> Restarted { get; } = new();

        /// <summary>Si no es null, ese paso falla con este resultado (se apaga al asignar null).</summary>
        public (bool Retryable, string Error)? FailCreate { get; set; }
        public (bool Retryable, string Error)? FailEnv { get; set; }
        public (bool Retryable, string Error)? FailStart { get; set; }
        public (bool Retryable, string Error)? FailRestart { get; set; }

        public Task<CoolifyCallResult<string>> CreateServiceAsync(CoolifyServiceSpec spec, CancellationToken cancellationToken)
        {
            if (FailCreate is { } f)
            {
                return Task.FromResult(CoolifyCallResult<string>.Fail(f.Retryable ? 503 : 422, f.Error, f.Retryable));
            }
            Created.Add(spec);
            return Task.FromResult(CoolifyCallResult<string>.Ok($"svc-{Created.Count}", 201));
        }

        public Task<CoolifyCallResult<bool>> SetEnvAsync(string serviceUuid, string key, string value, CancellationToken cancellationToken)
        {
            if (FailEnv is { } f)
            {
                return Task.FromResult(CoolifyCallResult<bool>.Fail(f.Retryable ? 503 : 422, f.Error, f.Retryable));
            }
            Envs.RemoveAll(e => e.Uuid == serviceUuid && e.Key == key);
            Envs.Add((serviceUuid, key, value));
            return Task.FromResult(CoolifyCallResult<bool>.Ok(true));
        }

        public Task<CoolifyCallResult<bool>> StartServiceAsync(string serviceUuid, CancellationToken cancellationToken)
        {
            if (FailStart is { } f)
            {
                return Task.FromResult(CoolifyCallResult<bool>.Fail(f.Retryable ? 503 : 422, f.Error, f.Retryable));
            }
            Started.Add(serviceUuid);
            return Task.FromResult(CoolifyCallResult<bool>.Ok(true));
        }

        public Task<CoolifyCallResult<bool>> RestartServiceAsync(string serviceUuid, bool pullLatest, CancellationToken cancellationToken)
        {
            if (FailRestart is { } f)
            {
                return Task.FromResult(CoolifyCallResult<bool>.Fail(f.Retryable ? 503 : 422, f.Error, f.Retryable));
            }
            Restarted.Add((serviceUuid, pullLatest));
            return Task.FromResult(CoolifyCallResult<bool>.Ok(true));
        }

        public string? EnvValue(string key) => Envs.LastOrDefault(e => e.Key == key).Value;
    }

    internal sealed class FakeProvisioningSettings : IProvisioningSettings
    {
        public string? BaseDomain { get; set; } = "erp.midominio.pe";
        public string? ImageRepository { get; set; } = "ghcr.io/cuenta";
        public string? LicenseControlUrl { get; set; } = "https://control.midominio.pe/";
        public string? DefaultVersion { get; set; } = "1.0.0";
        public bool IsConfigured => !string.IsNullOrWhiteSpace(BaseDomain) && !string.IsNullOrWhiteSpace(ImageRepository) && !string.IsNullOrWhiteSpace(LicenseControlUrl);
    }

    /// <summary>Generador barato (el real crea un certificado RSA de 3072 bits): valores reconocibles para comprobar adónde van.</summary>
    internal sealed class FakeSecretsGenerator : IProvisioningSecretsGenerator
    {
        public InstanceSecrets Generate(string licenseKey, string licensePublicKey)
            => new("pg-pass-1", "jwt-secret-1", "Adm1n-Init!2026", "Y2VydA==", "cert-pass-1", licenseKey, licensePublicKey);
    }

    internal sealed class FakeSigner(bool configured = true) : ILicenseSigner
    {
        public bool IsConfigured => configured;
        public string PublicKey => configured ? "public-key-base64" : string.Empty;
        public string Sign(byte[] data) => "sig";
    }

    /// <summary>Cifrado de mentira reversible, con el prefijo de ISecretProtector, para comprobar que los secretos nunca quedan en claro.</summary>
    internal sealed class FakeProtector : ISecretProtector
    {
        public string Protect(string plaintext) => "dp1:" + Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(plaintext));
        public string Unprotect(string storedValue)
            => storedValue.StartsWith("dp1:") ? System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(storedValue[4..])) : storedValue;
        public bool IsProtected(string? storedValue) => storedValue?.StartsWith("dp1:") == true;
    }

    /// <summary>
    /// Aprovisionamiento en Coolify (fase C5): las reglas puras, el montaje que no llama a Coolify y guarda secretos cifrados, el
    /// despachador por pasos con reintentos que nunca crean dos servicios, la actualización de versión, la cancelación y el resguardo
    /// de instancias con stack.
    /// </summary>
    public class DeploymentTests
    {
        private const int Alfa = 1, ErpPlan = 10, Instance = 100;
        private static readonly DateOnly Today = new(2026, 11, 1);

        private readonly TestDatabase _database = new();
        private readonly TestCurrentUserService _user = TestCurrentUserService.User("soporte");
        private readonly TestClock _clock = new(Today);
        private readonly FakeCoolifyClient _coolify = new();
        private readonly FakeProvisioningSettings _settings = new();
        private readonly FakeProtector _protector = new();
        private ILicenseSigner _signer = new FakeSigner();

        public DeploymentTests()
        {
            _database.SeedAsync(db =>
            {
                db.Set<Customer>().Add(new Customer { Id = Alfa, Ruc = CustomerHandlersTests.RucAlfa, BusinessName = "ALFA SAC" });
                db.Set<Plan>().Add(new Plan { Id = ErpPlan, Product = PlanProduct.Erp, Code = "ERP", Name = "ERP Básico", MonthlyPrice = 350m });
                db.Set<Subscription>().Add(new Subscription
                {
                    CustomerId = Alfa, PlanId = ErpPlan, Product = PlanProduct.Erp, StartOn = Today.AddDays(-10), BillingDay = 1, GraceDays = 5,
                    Price = 350m, PaidUntil = Today.AddDays(20), CoveredUntil = Today.AddDays(20), Status = SubscriptionStatus.Active, IsActive = true
                });
                db.Set<ErpInstance>().Add(new ErpInstance
                {
                    Id = Instance, CustomerId = Alfa, Name = "Producción", LicenseKeyHash = "old-hash", LicenseKeyHint = "olds", IsActive = true
                });
            }).GetAwaiter().GetResult();
        }

        private async Task<T> SendAsync<T>(Func<IUnitOfWork<int>, Task<T>> send)
        {
            await using var db = _database.CreateContext(_user);
            return await send(HandlerKit.UnitOfWork(db, _user));
        }

        private static IStringLocalizer<DS.Platform.Shared.Localization.ServerCommonResources> Localizer => new TestLocalizer();

        private Task<Result<DS.Platform.Application.DTOs.Deployments.Response.ProvisionInstanceResponseDto>> ProvisionAsync(
            string subdomain = "alfa", string version = "1.0.0", int instanceId = Instance)
            => SendAsync(uow => new ProvisionInstanceCommandHandler(uow, _clock, _settings, new FakeSecretsGenerator(), _signer, _protector, Localizer)
                .Handle(new ProvisionInstanceCommand
                {
                    ProvisionInstanceRequestDto = new ProvisionInstanceRequestDto { InstanceId = instanceId, Subdomain = subdomain, Version = version }
                }, CancellationToken.None));

        private Task<Result<int>> UpgradeAsync(string version)
            => SendAsync(uow => new UpgradeInstanceCommandHandler(uow, _clock, _settings, Localizer)
                .Handle(new UpgradeInstanceCommand { UpgradeInstanceRequestDto = new UpgradeInstanceRequestDto { InstanceId = Instance, Version = version } }, CancellationToken.None));

        private Task<Result<int>> DispatchAsync()
            => SendAsync(uow => new DispatchDeploymentsCommandHandler(uow, _coolify, _settings, _protector, _clock)
                .Handle(new DispatchDeploymentsCommand(), CancellationToken.None));

        private async Task<InstanceDeployment> DeploymentAsync(int id = 1)
        {
            await using var db = _database.CreateContext(_user);
            return await db.Set<InstanceDeployment>().AsNoTracking().SingleAsync(d => d.Id == id);
        }

        private async Task<ErpInstance> InstanceAsync()
        {
            await using var db = _database.CreateContext(_user);
            return await db.Set<ErpInstance>().AsNoTracking().SingleAsync(i => i.Id == Instance);
        }

        /// <summary>Monta, ejecuta y deja el stack arrancado (hasta Hecho).</summary>
        private async Task ProvisionAndDeployAsync()
        {
            Assert.True((await ProvisionAsync()).Succeeded);
            await DispatchAsync();
            Assert.Equal(RemoteActionStatus.Done, (await DeploymentAsync()).Status);
        }

        // ------------------------------------------------------------------ reglas puras

        [Theory]
        [InlineData("alfa", true)]
        [InlineData("  ALFA  ", true)]
        [InlineData("mi-empresa-2", true)]
        [InlineData("a", false)]
        [InlineData("-alfa", false)]
        [InlineData("alfa-", false)]
        [InlineData("al_fa", false)]
        [InlineData("al fa", false)]
        [InlineData("alfa.beta", false)]
        [InlineData("admin", false)]
        [InlineData("api-sunat", false)]
        [InlineData("", false)]
        public void The_subdomain_is_normalised_and_reserved_names_are_refused(string text, bool valid)
        {
            var error = ProvisioningRules.ValidateSubdomain(text, out var normalized);
            Assert.Equal(valid, error is null);
            Assert.Equal(text.Trim().ToLowerInvariant(), normalized);
        }

        [Theory]
        [InlineData("1.4.0", true)]
        [InlineData("2026.11.24-rc1", true)]
        [InlineData("v1_2", true)]
        [InlineData("latest", false)]
        [InlineData("LATEST", false)]
        [InlineData("", false)]
        [InlineData("1.0 0", false)]
        [InlineData("1.0/../x", false)]
        [InlineData("-1", false)]
        public void The_version_is_a_traceable_image_tag(string text, bool valid)
            => Assert.Equal(valid, ProvisioningRules.ValidateVersion(text, out _) is null);

        [Fact]
        public void The_host_and_service_name_come_from_the_subdomain()
        {
            Assert.Equal("alfa.erp.midominio.pe", ProvisioningRules.HostFor("alfa", " ERP.Midominio.pe. "));
            Assert.Equal("https://alfa.erp.midominio.pe", ProvisioningRules.UrlFor("alfa.erp.midominio.pe"));
            Assert.Equal("erp-alfa-100", ProvisioningRules.ServiceName("alfa.erp.midominio.pe", 100));
            Assert.Equal("alfa.erp.midominio.pe", ProvisioningRules.HostOf("https://Alfa.erp.midominio.pe/"));
            Assert.Null(ProvisioningRules.HostOf("no es una url"));
        }

        [Fact]
        public void The_compose_has_no_build_no_published_ports_and_takes_everything_from_variables()
        {
            var yaml = InstanceStack.ComposeYaml;
            Assert.DoesNotContain("build:", yaml);
            Assert.DoesNotContain("ports:", yaml);
            foreach (var image in InstanceStack.ImageNames)
            {
                Assert.Contains($"${{ERP_IMAGE_REPO}}/{image}:${{ERP_VERSION}}", yaml);
            }
            // Cada variable que usa el compose la carga el entorno (si falta una, Coolify arrancaría el stack con un valor vacío).
            var secrets = new FakeSecretsGenerator().Generate("lic_x", "pub");
            var env = InstanceStack.Environment(secrets, _settings, "alfa.erp.midominio.pe", "1.0.0");
            var used = System.Text.RegularExpressions.Regex.Matches(yaml, @"\$\{([A-Z_]+)\}").Select(m => m.Groups[1].Value).Distinct();
            foreach (var name in used)
            {
                Assert.True(env.ContainsKey(name), $"El compose usa ${{{name}}} y el entorno no la carga.");
                Assert.False(string.IsNullOrWhiteSpace(env[name]), $"{name} no puede ir vacía.");
            }
            // Los secretos van entre comillas: una contraseña que empiece con ! * o @ rompería el YAML.
            Assert.Contains("\"${ERP_INITIAL_ADMIN_PASSWORD}\"", yaml);
            Assert.Contains("\"${JWT_SECRET}\"", yaml);
            Assert.Equal("https://control.midominio.pe", env["LICENSE_CONTROL_URL"]);
            Assert.Equal("ghcr.io/cuenta", env["ERP_IMAGE_REPO"]);
        }

        [Fact]
        public void The_generated_secrets_follow_the_ERP_policy_and_the_certificate_loads()
        {
            for (var i = 0; i < 50; i++)
            {
                var password = ProvisioningSecretsGenerator.InitialPassword();
                Assert.InRange(password.Length, 8, 16);
                Assert.Contains(password, char.IsUpper);
                Assert.Contains(password, char.IsLower);
                Assert.Contains(password, char.IsDigit);
                Assert.Contains(password, c => !char.IsLetterOrDigit(c));
                Assert.True(char.IsLetterOrDigit(password[0]), "No puede empezar con un símbolo (rompería el YAML).");
                Assert.DoesNotContain(password, c => c is '$' or '"' or '\'' or '\\' or '#' or ' ');
            }

            var secrets = new ProvisioningSecretsGenerator().Generate("lic_x", "pub");
            Assert.Equal("lic_x", secrets.LicenseKey);
            Assert.NotEqual(secrets.PostgresPassword, secrets.DataProtectionCertificatePassword);
            // El mismo formato que lee el ERP: un .pfx en Base64 con su contraseña.
            using var certificate = X509CertificateLoader.LoadPkcs12(
                Convert.FromBase64String(secrets.DataProtectionCertificateBase64), secrets.DataProtectionCertificatePassword);
            Assert.True(certificate.HasPrivateKey);
            Assert.True(certificate.NotAfter > DateTime.UtcNow.AddYears(9));

            var other = new ProvisioningSecretsGenerator().Generate("lic_y", "pub");
            Assert.NotEqual(secrets.JwtSecret, other.JwtSecret);
            Assert.NotEqual(secrets.DataProtectionCertificateBase64, other.DataProtectionCertificateBase64);
        }

        [Fact]
        public void The_provisioning_settings_need_an_https_license_url()
        {
            static ProvisioningSettings With(string? control) => new(Options.Create(new ProvisioningOptions
            {
                BaseDomain = "erp.x.pe", ImageRepository = "ghcr.io/c", LicenseControlUrl = control
            }));
            Assert.True(With("https://control.x.pe").IsConfigured);
            Assert.False(With("http://control.x.pe").IsConfigured);
            Assert.False(With(null).IsConfigured);
            Assert.False(new ProvisioningSettings(Options.Create(new ProvisioningOptions())).IsConfigured);
        }

        // ------------------------------------------------------------------ montar

        [Fact]
        public async Task Provisioning_saves_the_deployment_without_calling_coolify_and_keeps_secrets_encrypted()
        {
            var result = await ProvisionAsync();

            Assert.True(result.Succeeded, string.Join(" ", result.Messages));
            Assert.Equal("https://alfa.erp.midominio.pe", result.Data.Url);
            Assert.Equal("administrador", result.Data.InitialAdminUser);
            Assert.Equal("Adm1n-Init!2026", result.Data.InitialAdminPassword);
            // Ninguna llamada externa dentro del caso de uso: solo la bandeja.
            Assert.Empty(_coolify.Created);

            var deployment = await DeploymentAsync();
            Assert.Equal(DeploymentType.Create, deployment.Type);
            Assert.Equal(DeploymentStep.None, deployment.Step);
            Assert.Equal(RemoteActionStatus.Pending, deployment.Status);
            Assert.Equal("1.0.0", deployment.Version);
            Assert.StartsWith("dp1:", deployment.SecretsProtected);
            Assert.DoesNotContain("pg-pass-1", deployment.SecretsProtected);
            Assert.DoesNotContain("Adm1n-Init", deployment.SecretsProtected);

            var instance = await InstanceAsync();
            Assert.Equal("https://alfa.erp.midominio.pe", instance.Url);
            Assert.Equal("Coolify", instance.Server);
            // La clave anterior deja de valer y la nueva solo existe como hash (más el cifrado del despliegue).
            Assert.NotEqual("old-hash", instance.LicenseKeyHash);
            Assert.Equal(64, instance.LicenseKeyHash.Length);
            var stored = ProvisioningSecretsJson.Deserialize(_protector.Unprotect(deployment.SecretsProtected!))!;
            Assert.Equal(LicenseKeys.Hash(stored.LicenseKey), instance.LicenseKeyHash);
            Assert.Equal(LicenseKeys.Hint(stored.LicenseKey), instance.LicenseKeyHint);
            Assert.Equal("public-key-base64", stored.LicensePublicKey);
            Assert.Null(instance.CoolifyServiceUuid);
        }

        [Fact]
        public async Task Provisioning_is_refused_when_it_is_not_safe()
        {
            _settings.BaseDomain = null;
            Assert.Equal(ProvisioningRules.NotConfiguredMessage, (await ProvisionAsync()).Messages.Single());
            _settings.BaseDomain = "erp.midominio.pe";

            _signer = new FakeSigner(configured: false);
            Assert.Equal(ProvisionInstanceCommandHandler.NoSigningKeyMessage, (await ProvisionAsync()).Messages.Single());
            _signer = new FakeSigner();

            Assert.Equal(ProvisioningRules.InvalidSubdomainMessage, (await ProvisionAsync(subdomain: "Mi Empresa")).Messages.Single());
            Assert.Equal(ProvisioningRules.ReservedSubdomainMessage, (await ProvisionAsync(subdomain: "admin")).Messages.Single());
            Assert.Equal(ProvisioningRules.InvalidVersionMessage, (await ProvisionAsync(version: "latest")).Messages.Single());
            Assert.Equal(ProvisionInstanceCommandHandler.InstanceNotFoundMessage, (await ProvisionAsync(instanceId: 999)).Messages.Single());

            // Una instancia que ya se conectó está montada a mano: la consola no la duplica ni le cambia la clave.
            await _database.SeedAsync(db => db.Set<ErpInstance>().Single().LastContactOnUtc = DateTime.UtcNow);
            Assert.Equal(ProvisionInstanceCommandHandler.AlreadyConnectedMessage, (await ProvisionAsync()).Messages.Single());
            Assert.Equal("old-hash", (await InstanceAsync()).LicenseKeyHash);
            await _database.SeedAsync(db => db.Set<ErpInstance>().Single().LastContactOnUtc = null);

            // Sin suscripción vigente del ERP nacería en solo lectura.
            await _database.SeedAsync(db => db.Set<Subscription>().Single().Status = SubscriptionStatus.Cancelled);
            Assert.Equal(ProvisionInstanceCommandHandler.NoSubscriptionMessage, (await ProvisionAsync()).Messages.Single());
            Assert.Empty(await _database.CreateContext(_user).Set<InstanceDeployment>().ToListAsync());
        }

        [Fact]
        public async Task Two_deployments_cannot_be_open_for_one_instance_and_the_address_cannot_repeat()
        {
            Assert.True((await ProvisionAsync()).Succeeded);
            Assert.Equal(ProvisionInstanceCommandHandler.OpenDeploymentMessage, (await ProvisionAsync(subdomain: "otro")).Messages.Single());

            // Otra instancia no puede quedarse con la misma dirección.
            await _database.SeedAsync(db => db.Set<ErpInstance>().Add(new ErpInstance
            {
                Id = 101, CustomerId = Alfa, Name = "QA", LicenseKeyHash = "h2", LicenseKeyHint = "h2h2", IsActive = true
            }));
            Assert.Equal(ProvisioningRules.HostTakenMessage, (await ProvisionAsync(instanceId: 101)).Messages.Single());
            Assert.True((await ProvisionAsync(subdomain: "qa", instanceId: 101)).Succeeded);
        }

        // ------------------------------------------------------------------ despachador

        [Fact]
        public async Task The_dispatcher_creates_the_service_loads_every_variable_and_starts_it()
        {
            Assert.True((await ProvisionAsync()).Succeeded);
            var dispatched = await DispatchAsync();

            Assert.Equal(1, dispatched.Data);
            var spec = Assert.Single(_coolify.Created);
            Assert.Equal("erp-alfa-100", spec.Name);
            Assert.Equal(InstanceStack.ComposeYaml, spec.ComposeYaml);
            Assert.Equal("web", spec.ServiceName);
            Assert.Equal("https://alfa.erp.midominio.pe", spec.PublicUrl);
            Assert.Equal(80, spec.ServicePort);

            Assert.Equal("svc-1", Assert.Single(_coolify.Started));
            Assert.Equal("pg-pass-1", _coolify.EnvValue("POSTGRES_PASSWORD"));
            Assert.Equal("Adm1n-Init!2026", _coolify.EnvValue("ERP_INITIAL_ADMIN_PASSWORD"));
            Assert.Equal("1.0.0", _coolify.EnvValue("ERP_VERSION"));
            Assert.Equal("https://alfa.erp.midominio.pe", _coolify.EnvValue("ERP_PUBLIC_URL"));
            Assert.StartsWith("lic_", _coolify.EnvValue("LICENSE_KEY"));
            Assert.Equal("public-key-base64", _coolify.EnvValue("LICENSE_PUBLIC_KEY"));

            var deployment = await DeploymentAsync();
            Assert.Equal(RemoteActionStatus.Done, deployment.Status);
            Assert.Equal(DeploymentStep.Started, deployment.Step);
            Assert.NotNull(deployment.DoneOnUtc);
            // Terminado el montaje, los secretos solo viven en Coolify.
            Assert.Null(deployment.SecretsProtected);

            var instance = await InstanceAsync();
            Assert.Equal("svc-1", instance.CoolifyServiceUuid);
            Assert.Equal("1.0.0", instance.DeployedVersion);
            // La clave que recibió el stack es la que la consola reconoce.
            Assert.Equal(LicenseKeys.Hash(_coolify.EnvValue("LICENSE_KEY")!), instance.LicenseKeyHash);
        }

        [Fact]
        public async Task A_failure_after_the_service_exists_never_creates_a_second_one()
        {
            Assert.True((await ProvisionAsync()).Succeeded);
            _coolify.FailEnv = (true, "Coolify no respondió a tiempo.");

            Assert.Equal(0, (await DispatchAsync()).Data);
            var failed = await DeploymentAsync();
            Assert.Equal(RemoteActionStatus.Failed, failed.Status);
            Assert.Equal(DeploymentStep.ServiceCreated, failed.Step);
            Assert.Contains("POSTGRES_DB", failed.LastError);   // dice qué variable falló (nunca su valor)
            Assert.Equal(1, failed.Attempts);
            Assert.NotNull(failed.SecretsProtected);   // hacen falta para reintentar
            Assert.Equal("svc-1", (await InstanceAsync()).CoolifyServiceUuid);
            Assert.Null((await InstanceAsync()).DeployedVersion);

            // Todavía no toca reintentar: espera su hora.
            Assert.Equal("No hay despliegues pendientes.", (await DispatchAsync()).Messages.Single());

            _coolify.FailEnv = null;
            _clock.Advance(TimeSpan.FromMinutes(2));
            Assert.Equal(1, (await DispatchAsync()).Data);

            Assert.Single(_coolify.Created);   // un solo servicio
            Assert.Single(_coolify.Started);
            var done = await DeploymentAsync();
            Assert.Equal(RemoteActionStatus.Done, done.Status);
            Assert.Equal(2, done.Attempts);
            Assert.Null(done.SecretsProtected);
        }

        [Fact]
        public async Task A_failed_start_resumes_at_the_start_without_touching_the_variables_again()
        {
            Assert.True((await ProvisionAsync()).Succeeded);
            _coolify.FailStart = (true, "Coolify respondió HTTP 503.");
            await DispatchAsync();
            Assert.Equal(DeploymentStep.Configured, (await DeploymentAsync()).Step);

            var envCount = _coolify.Envs.Count;
            var licenseBefore = _coolify.EnvValue("LICENSE_KEY");
            _coolify.FailStart = null;
            _coolify.Envs.Clear();   // si las volviera a cargar se vería
            _clock.Advance(TimeSpan.FromMinutes(2));
            await DispatchAsync();

            Assert.Empty(_coolify.Envs);
            Assert.Equal(13, envCount);
            Assert.NotNull(licenseBefore);
            Assert.Single(_coolify.Created);
            Assert.Equal(RemoteActionStatus.Done, (await DeploymentAsync()).Status);
        }

        [Fact]
        public async Task A_rejection_that_retrying_cannot_fix_is_abandoned_and_can_be_retried_by_hand()
        {
            Assert.True((await ProvisionAsync()).Succeeded);
            _coolify.FailCreate = (false, "Coolify: Domain conflicts detected.");
            await DispatchAsync();

            var abandoned = await DeploymentAsync();
            Assert.Equal(RemoteActionStatus.Abandoned, abandoned.Status);
            Assert.Contains("Domain conflicts", abandoned.LastError);
            Assert.Equal(DeploymentStep.None, abandoned.Step);
            Assert.Null((await InstanceAsync()).CoolifyServiceUuid);

            // Abandonado cuenta como abierto: no se puede montar otro encima.
            Assert.Equal(ProvisionInstanceCommandHandler.OpenDeploymentMessage, (await ProvisionAsync(subdomain: "otro")).Messages.Single());

            _coolify.FailCreate = null;
            var retry = await SendAsync(uow => new RetryDeploymentCommandHandler(uow, _clock, Localizer).Handle(new RetryDeploymentCommand { Id = 1 }, CancellationToken.None));
            Assert.True(retry.Succeeded);
            var pending = await DeploymentAsync();
            Assert.Equal(RemoteActionStatus.Pending, pending.Status);
            Assert.Equal(0, pending.Attempts);

            await DispatchAsync();
            Assert.Equal(RemoteActionStatus.Done, (await DeploymentAsync()).Status);
        }

        [Fact]
        public async Task Retries_are_exhausted_after_ten_attempts()
        {
            Assert.True((await ProvisionAsync()).Succeeded);
            _coolify.FailCreate = (true, "No se pudo conectar con Coolify.");

            for (var i = 0; i < 10; i++)
            {
                _clock.Advance(TimeSpan.FromHours(7));
                await DispatchAsync();
            }

            var deployment = await DeploymentAsync();
            Assert.Equal(RemoteActionStatus.Abandoned, deployment.Status);
            Assert.Equal(10, deployment.Attempts);
        }

        [Fact]
        public async Task A_dead_dispatcher_does_not_lose_the_deployment()
        {
            Assert.True((await ProvisionAsync()).Succeeded);
            // Un despachador lo tomó (En curso) y murió: su bloqueo vence y otro lo retoma.
            await _database.SeedAsync(db =>
            {
                var d = db.Set<InstanceDeployment>().Single();
                d.Status = RemoteActionStatus.Sending;
                d.LockedUntilUtc = _clock.NowUtc.AddMinutes(5);
            });
            Assert.Equal("No hay despliegues pendientes.", (await DispatchAsync()).Messages.Single());

            _clock.Advance(TimeSpan.FromMinutes(6));
            Assert.Equal(1, (await DispatchAsync()).Data);
            Assert.Equal(RemoteActionStatus.Done, (await DeploymentAsync()).Status);
        }

        [Fact]
        public async Task Lost_secrets_abandon_the_deployment_instead_of_inventing_new_ones()
        {
            Assert.True((await ProvisionAsync()).Succeeded);
            await _database.SeedAsync(db => db.Set<InstanceDeployment>().Single().SecretsProtected = null);

            await DispatchAsync();

            var deployment = await DeploymentAsync();
            Assert.Equal(RemoteActionStatus.Abandoned, deployment.Status);
            Assert.Equal(DispatchDeploymentsCommandHandler.SecretsMissingMessage, deployment.LastError);
            Assert.Empty(_coolify.Created);
        }

        [Fact]
        public async Task Without_provisioning_settings_the_deployment_waits()
        {
            Assert.True((await ProvisionAsync()).Succeeded);
            _settings.ImageRepository = null;

            await DispatchAsync();

            var deployment = await DeploymentAsync();
            Assert.Equal(RemoteActionStatus.Failed, deployment.Status);
            Assert.Equal(ProvisioningRules.NotConfiguredMessage, deployment.LastError);
            Assert.Empty(_coolify.Created);
        }

        // ------------------------------------------------------------------ actualizar

        [Fact]
        public async Task Upgrading_changes_the_version_variable_and_restarts_pulling_the_images()
        {
            await ProvisionAndDeployAsync();
            Assert.Equal("1.0.0", _coolify.EnvValue("ERP_VERSION"));

            Assert.True((await UpgradeAsync("1.1.0")).Succeeded);
            Assert.Empty(_coolify.Restarted);   // el caso de uso no llama a Coolify
            await DispatchAsync();

            Assert.Equal("1.1.0", _coolify.EnvValue("ERP_VERSION"));
            Assert.Equal(("svc-1", true), Assert.Single(_coolify.Restarted));
            Assert.Single(_coolify.Created);
            Assert.Equal("1.1.0", (await InstanceAsync()).DeployedVersion);
            var upgrade = await DeploymentAsync(2);
            Assert.Equal(DeploymentType.Update, upgrade.Type);
            Assert.Equal(RemoteActionStatus.Done, upgrade.Status);
        }

        [Fact]
        public async Task A_failed_restart_is_retried_and_keeps_the_old_version_recorded_until_it_works()
        {
            await ProvisionAndDeployAsync();
            Assert.True((await UpgradeAsync("1.1.0")).Succeeded);
            _coolify.FailRestart = (true, "Coolify respondió HTTP 502.");

            await DispatchAsync();
            Assert.Equal("1.0.0", (await InstanceAsync()).DeployedVersion);
            Assert.Equal(RemoteActionStatus.Failed, (await DeploymentAsync(2)).Status);

            _coolify.FailRestart = null;
            _clock.Advance(TimeSpan.FromMinutes(2));
            await DispatchAsync();
            Assert.Equal("1.1.0", (await InstanceAsync()).DeployedVersion);
        }

        [Fact]
        public async Task Upgrading_is_refused_for_unprovisioned_instances_the_same_version_and_open_deployments()
        {
            Assert.Equal(UpgradeInstanceCommandHandler.NotProvisionedMessage, (await UpgradeAsync("1.1.0")).Messages.Single());

            Assert.True((await ProvisionAsync()).Succeeded);
            // El montaje aún no terminó: tampoco se actualiza.
            Assert.Equal(UpgradeInstanceCommandHandler.NotProvisionedMessage, (await UpgradeAsync("1.1.0")).Messages.Single());
            await DispatchAsync();

            Assert.Equal(UpgradeInstanceCommandHandler.SameVersionMessage, (await UpgradeAsync("1.0.0")).Messages.Single());
            Assert.Equal(ProvisioningRules.InvalidVersionMessage, (await UpgradeAsync("latest")).Messages.Single());

            Assert.True((await UpgradeAsync("1.1.0")).Succeeded);
            Assert.Equal(UpgradeInstanceCommandHandler.OpenDeploymentMessage, (await UpgradeAsync("1.2.0")).Messages.Single());
        }

        // ------------------------------------------------------------------ cancelar y borrar

        [Fact]
        public async Task Cancelling_before_anything_was_created_wipes_the_secrets()
        {
            Assert.True((await ProvisionAsync()).Succeeded);

            var cancel = await SendAsync(uow => new CancelDeploymentCommandHandler(uow).Handle(new CancelDeploymentCommand { Id = 1 }, CancellationToken.None));

            Assert.True(cancel.Succeeded);
            var deployment = await DeploymentAsync();
            Assert.Equal(RemoteActionStatus.Cancelled, deployment.Status);
            Assert.Null(deployment.SecretsProtected);
            await DispatchAsync();
            Assert.Empty(_coolify.Created);

            // Cancelado ya no cuenta como abierto: se puede montar de nuevo.
            Assert.True((await ProvisionAsync(subdomain: "alfa2")).Succeeded);
        }

        [Fact]
        public async Task Cancelling_after_the_service_exists_frees_the_instance_and_names_the_orphan_service()
        {
            Assert.True((await ProvisionAsync()).Succeeded);
            _coolify.FailEnv = (true, "No se pudo conectar con Coolify.");
            await DispatchAsync();
            Assert.Equal("svc-1", (await InstanceAsync()).CoolifyServiceUuid);

            var cancel = await SendAsync(uow => new CancelDeploymentCommandHandler(uow).Handle(new CancelDeploymentCommand { Id = 1 }, CancellationToken.None));

            Assert.True(cancel.Succeeded);
            Assert.Contains("svc-1", cancel.Messages.Single());   // dice cuál eliminar en Coolify
            Assert.Null((await InstanceAsync()).CoolifyServiceUuid);
        }

        [Fact]
        public async Task Only_pending_failed_or_abandoned_deployments_are_cancelled_or_retried()
        {
            await ProvisionAndDeployAsync();

            var cancel = await SendAsync(uow => new CancelDeploymentCommandHandler(uow).Handle(new CancelDeploymentCommand { Id = 1 }, CancellationToken.None));
            Assert.Equal(CancelDeploymentCommandHandler.NotCancellableMessage, cancel.Messages.Single());
            var retry = await SendAsync(uow => new RetryDeploymentCommandHandler(uow, _clock, Localizer).Handle(new RetryDeploymentCommand { Id = 1 }, CancellationToken.None));
            Assert.Equal(RetryDeploymentCommandHandler.NotRetryableMessage, retry.Messages.Single());
            Assert.Equal(CancelDeploymentCommandHandler.NotFoundMessage,
                (await SendAsync(uow => new CancelDeploymentCommandHandler(uow).Handle(new CancelDeploymentCommand { Id = 99 }, CancellationToken.None))).Messages.Single());
        }

        [Fact]
        public async Task An_instance_with_a_stack_or_an_open_deployment_cannot_be_deleted()
        {
            Task<Result<int>> Delete() => SendAsync(uow => new DeleteInstanceCommandHandler(uow, Localizer).Handle(new DeleteInstanceCommand { Id = Instance }, CancellationToken.None));

            Assert.True((await ProvisionAsync()).Succeeded);
            Assert.Equal(DeleteInstanceCommandHandler.OpenDeploymentMessage, (await Delete()).Messages.Single());

            await DispatchAsync();
            Assert.Equal(DeleteInstanceCommandHandler.HasStackMessage, (await Delete()).Messages.Single());
            Assert.False((await InstanceAsync()).IsDeleted);
        }
    }
}
