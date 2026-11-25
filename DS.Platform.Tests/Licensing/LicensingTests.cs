using System.Text;
using System.Text.Json;
using DS.Platform.Application.DTOs.Instances.Request;
using DS.Platform.Application.DTOs.Instances.Response;
using DS.Platform.Application.Features.Customers.Commands.Delete;
using DS.Platform.Application.Features.Instances.Commands.Create;
using DS.Platform.Application.Features.Instances.Commands.Delete;
using DS.Platform.Application.Features.Instances.Commands.RotateKey;
using DS.Platform.Application.Features.Instances.Commands.Update;
using DS.Platform.Application.Features.Instances.Common;
using DS.Platform.Application.Features.Instances.Queries.GetAllPaged;
using DS.Platform.Application.Features.Instances.Validators;
using DS.Platform.Application.Features.Licensing.Commands.Check;
using DS.Platform.Application.Interfaces.Repositories;
using DS.Platform.Application.Interfaces.Services.Licensing;
using DS.Platform.Domain.Entities.Billing;
using DS.Platform.Domain.Entities.Customers;
using DS.Platform.Domain.Entities.Erp;
using DS.Platform.Domain.Enums;
using DS.Platform.Infrastructure.Shared.Services.Licensing;
using DS.Platform.Shared.Wrapper;
using DS.Platform.Tests.Customers;
using DS.Platform.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Org.BouncyCastle.Math.EC.Rfc8032;

namespace DS.Platform.Tests.Licensing
{
    /// <summary>
    /// Licencias de las instancias del ERP (fase C4): el estado se deriva de la suscripción del ERP del cliente, la respuesta va
    /// firmada con Ed25519 y repite el nonce de la instancia, la clave solo se guarda como hash y una clave desconocida no revela nada.
    /// </summary>
    public class LicensingTests
    {
        private const int Alfa = 1, Beta = 2, ErpBasic = 10, ApiPlan = 20;
        private static readonly DateOnly Today = new(2026, 11, 1);
        private const string Nonce = "abcdefghijklmnop0123456789";

        private readonly TestDatabase _database = new();
        private readonly TestCurrentUserService _user = TestCurrentUserService.User("cobranzas");
        private readonly TestClock _clock = new(Today);
        private readonly byte[] _seed = Enumerable.Range(1, 32).Select(i => (byte)i).ToArray();
        private readonly ILicenseSigner _signer;

        public LicensingTests()
        {
            _signer = new Ed25519LicenseSigner(Options.Create(new LicenseSettings { SigningKey = Convert.ToBase64String(_seed) }));
            _database.SeedAsync(db =>
            {
                db.Set<Customer>().AddRange(
                    new Customer { Id = Alfa, Ruc = CustomerHandlersTests.RucAlfa, BusinessName = "ALFA SAC" },
                    new Customer { Id = Beta, Ruc = CustomerHandlersTests.RucBeta, BusinessName = "BETA SAC" });
                db.Set<Plan>().AddRange(
                    new Plan { Id = ErpBasic, Product = PlanProduct.Erp, Code = "ERP-BASICO", Name = "ERP Básico", MonthlyPrice = 350m,
                               MaxCompanies = 2, MaxUsers = 10, MaxBranches = 4, IncludesAi = true },
                    new Plan { Id = ApiPlan, Product = PlanProduct.ApiSunat, Code = "API-1000", Name = "API 1000", MonthlyPrice = 60m });
            }).GetAwaiter().GetResult();
        }

        private async Task<T> SendAsync<T>(Func<IUnitOfWork<int>, Task<T>> send)
        {
            await using var db = _database.CreateContext(_user);
            return await send(HandlerKit.UnitOfWork(db, _user));
        }

        private Task SubscribeAsync(int customer, int plan, SubscriptionStatus status, DateOnly? covered = null, DateOnly? suspendOn = null)
            => _database.SeedAsync(db => db.Set<Subscription>().Add(new Subscription
            {
                CustomerId = customer, PlanId = plan, Product = plan == ApiPlan ? PlanProduct.ApiSunat : PlanProduct.Erp, StartOn = Today.AddDays(-20),
                BillingDay = 1, Status = status, CoveredUntil = covered ?? Today.AddDays(30), SuspendOn = suspendOn, IsActive = true
            }));

        private async Task<InstanceKeyResponseDto> CreateInstanceAsync(int customer = Alfa, string name = "Producción")
        {
            var result = await SendAsync(uow => new CreateInstanceCommandHandler(uow, _clock, new TestLocalizer()).Handle(
                new CreateInstanceCommand { CreateInstanceRequestDto = new CreateInstanceRequestDto { CustomerId = customer, Name = name, Url = "https://alfa.erp.pe" } },
                CancellationToken.None));
            Assert.True(result.Succeeded, string.Join(" ", result.Messages));
            return result.Data;
        }

        private Task<Result<LicenseEnvelopeDto>> CheckAsync(string? key, LicenseCheckRequestDto? report = null) => SendAsync(uow =>
            new CheckLicenseCommandHandler(uow, _clock, _signer).Handle(new CheckLicenseCommand
            {
                LicenseKey = key, LicenseCheckRequestDto = report ?? new LicenseCheckRequestDto { Nonce = Nonce }
            }, CancellationToken.None));

        private static JsonElement Payload(LicenseEnvelopeDto envelope) => JsonDocument.Parse(envelope.Payload).RootElement;

        // ---------------------------------------------------------------- reglas puras
        [Fact]
        public void The_license_follows_the_erp_subscription_of_the_customer()
        {
            static Subscription Sub(PlanProduct product, SubscriptionStatus status, Plan? plan = null) => new()
            {
                Id = 1, CustomerId = 1, PlanId = 1, Product = product, Status = status, Plan = plan,
                CoveredUntil = Today.AddDays(30), SuspendOn = Today.AddDays(35)
            };
            var erpPlan = new Plan { Product = PlanProduct.Erp, Code = "ERP-PRO", Name = "x", MaxCompanies = 3, MaxUsers = null, MaxBranches = 5, IncludesAi = true };

            var active = LicenseStateBuilder.Build([Sub(PlanProduct.Erp, SubscriptionStatus.Trial, erpPlan)], Today);
            Assert.Equal((LicenseStatus.Active, "ERP-PRO", 3, (int?)null, 5, true), (active.Status, active.PlanCode, active.MaxCompanies, active.MaxUsers, active.MaxBranches, active.Ai));
            Assert.Null(active.Message);

            Assert.Equal(LicenseStatus.Grace, LicenseStateBuilder.Build([Sub(PlanProduct.Erp, SubscriptionStatus.Grace)], Today).Status);
            Assert.Equal(LicenseStatus.Suspended, LicenseStateBuilder.Build([Sub(PlanProduct.Erp, SubscriptionStatus.Suspended)], Today).Status);
            Assert.Equal(LicenseStateBuilder.SuspendedMessage, LicenseStateBuilder.Build([Sub(PlanProduct.Erp, SubscriptionStatus.Suspended)], Today).Message);

            // Solo cuenta la suscripción del ERP: una de la API SUNAT al día no activa la licencia del ERP.
            var onlyApi = LicenseStateBuilder.Build([Sub(PlanProduct.ApiSunat, SubscriptionStatus.Active)], Today);
            Assert.Equal((LicenseStatus.Suspended, LicenseStateBuilder.NoSubscriptionMessage), (onlyApi.Status, onlyApi.Message));

            var cancelled = LicenseStateBuilder.Build([Sub(PlanProduct.Erp, SubscriptionStatus.Cancelled)], Today);
            Assert.Equal(LicenseStatus.Cancelled, cancelled.Status);

            // Una cancelada y otra vigente: manda la vigente.
            var reborn = new Subscription { Id = 2, CustomerId = 1, PlanId = 1, Product = PlanProduct.Erp, Status = SubscriptionStatus.Active, CoveredUntil = Today.AddDays(9) };
            Assert.Equal(LicenseStatus.Active, LicenseStateBuilder.Build([Sub(PlanProduct.Erp, SubscriptionStatus.Cancelled), reborn], Today).Status);
        }

        [Fact]
        public void The_renewal_warning_starts_five_days_before_the_due_date()
        {
            Subscription Sub(int days) => new() { CustomerId = 1, PlanId = 1, Product = PlanProduct.Erp, Status = SubscriptionStatus.Active, CoveredUntil = Today.AddDays(days) };

            Assert.Null(LicenseStateBuilder.Build([Sub(6)], Today).Message);
            Assert.Contains("vence el", LicenseStateBuilder.Build([Sub(5)], Today).Message);
            Assert.Contains("vence el", LicenseStateBuilder.Build([Sub(0)], Today).Message);
        }

        [Fact]
        public void Keys_are_random_prefixed_and_only_the_hash_is_comparable()
        {
            var a = LicenseKeys.Generate();
            var b = LicenseKeys.Generate();

            Assert.NotEqual(a, b);
            Assert.StartsWith(LicenseKeys.Prefix, a);
            Assert.Equal(64, LicenseKeys.Hash(a).Length);
            Assert.Equal(LicenseKeys.Hash(a), LicenseKeys.Hash(a));
            Assert.NotEqual(LicenseKeys.Hash(a), LicenseKeys.Hash(b));
            Assert.Equal(a[^4..], LicenseKeys.Hint(a));
            Assert.DoesNotContain(a, LicenseKeys.Hash(a));
        }

        [Fact]
        public void Contact_levels_follow_the_ten_minute_cycle()
        {
            var now = new DateTime(2026, 11, 1, 12, 0, 0, DateTimeKind.Utc);
            Assert.Equal("never", InstanceContact.Describe(null, now).Level);
            Assert.Equal("online", InstanceContact.Describe(now.AddMinutes(-25), now).Level);
            Assert.Equal("delayed", InstanceContact.Describe(now.AddHours(-3), now).Level);
            Assert.Equal("lost", InstanceContact.Describe(now.AddDays(-2), now).Level);
        }

        // ---------------------------------------------------------------- firma
        [Fact]
        public void The_signature_verifies_with_the_public_key_and_breaks_if_the_payload_changes()
        {
            var data = Encoding.UTF8.GetBytes("{\"status\":\"Active\"}");
            var signature = Convert.FromBase64String(_signer.Sign(data));
            var publicKey = Convert.FromBase64String(_signer.PublicKey);

            Assert.Equal(32, publicKey.Length);
            Assert.True(Ed25519.Verify(signature, 0, publicKey, 0, data, 0, data.Length));
            var tampered = Encoding.UTF8.GetBytes("{\"status\":\"Suspd\"}");
            Assert.False(Ed25519.Verify(signature, 0, publicKey, 0, tampered, 0, tampered.Length));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("no es base64 !!")]
        [InlineData("AAAA")] // base64 válido pero de 3 bytes, no de 32
        public void Without_a_valid_key_the_signer_is_off_and_refuses_to_sign(string? key)
        {
            var signer = new Ed25519LicenseSigner(Options.Create(new LicenseSettings { SigningKey = key }));

            Assert.False(signer.IsConfigured);
            Assert.Equal(string.Empty, signer.PublicKey);
            Assert.Throws<InvalidOperationException>(() => signer.Sign([1]));
        }

        // ---------------------------------------------------------------- instancias
        [Fact]
        public async Task Creating_an_instance_returns_the_key_once_and_stores_only_its_hash()
        {
            var created = await CreateInstanceAsync();

            await using var db = _database.CreateContext(_user);
            var instance = await db.Set<ErpInstance>().AsNoTracking().SingleAsync();
            Assert.Equal(LicenseKeys.Hash(created.LicenseKey), instance.LicenseKeyHash);
            Assert.Equal(created.LicenseKey[^4..], instance.LicenseKeyHint);
            Assert.NotEqual(created.LicenseKey, instance.LicenseKeyHash);
            Assert.True(instance.IsActive);
            Assert.Null(instance.LastContactOnUtc);
        }

        [Fact]
        public async Task An_instance_name_is_unique_per_customer_and_the_customer_must_be_active()
        {
            await CreateInstanceAsync(Alfa, "Producción");
            await CreateInstanceAsync(Beta, "Producción"); // otro cliente: sí

            var duplicate = await SendAsync(uow => new CreateInstanceCommandHandler(uow, _clock, new TestLocalizer()).Handle(
                new CreateInstanceCommand { CreateInstanceRequestDto = new CreateInstanceRequestDto { CustomerId = Alfa, Name = "producción" } }, CancellationToken.None));
            Assert.Equal(CreateInstanceCommandHandler.DuplicateNameMessage, duplicate.Messages.Single());

            await _database.SeedAsync(db => db.Set<Customer>().Single(c => c.Id == Beta).IsActive = false);
            var inactive = await SendAsync(uow => new CreateInstanceCommandHandler(uow, _clock, new TestLocalizer()).Handle(
                new CreateInstanceCommand { CreateInstanceRequestDto = new CreateInstanceRequestDto { CustomerId = Beta, Name = "QA" } }, CancellationToken.None));
            Assert.Equal(CreateInstanceCommandHandler.CustomerInactiveMessage, inactive.Messages.Single());
        }

        [Fact]
        public async Task Rotating_the_key_invalidates_the_old_one_at_once()
        {
            await SubscribeAsync(Alfa, ErpBasic, SubscriptionStatus.Active);
            var created = await CreateInstanceAsync();
            Assert.True((await CheckAsync(created.LicenseKey)).Succeeded);

            var rotated = await SendAsync(uow => new RotateInstanceKeyCommandHandler(uow, _clock, new TestLocalizer())
                .Handle(new RotateInstanceKeyCommand { Id = created.Id }, CancellationToken.None));

            Assert.True(rotated.Succeeded);
            Assert.NotEqual(created.LicenseKey, rotated.Data.LicenseKey);
            Assert.Equal(CheckLicenseCommandHandler.InvalidKeyMessage, (await CheckAsync(created.LicenseKey)).Messages.Single());
            Assert.True((await CheckAsync(rotated.Data.LicenseKey)).Succeeded);
        }

        [Fact]
        public async Task A_deleted_instance_stops_receiving_licenses_and_updates_keep_the_key()
        {
            await SubscribeAsync(Alfa, ErpBasic, SubscriptionStatus.Active);
            var created = await CreateInstanceAsync();

            var update = await SendAsync(uow => new UpdateInstanceCommandHandler(uow, new TestLocalizer()).Handle(new UpdateInstanceCommand
            {
                UpdateInstanceRequestDto = new UpdateInstanceRequestDto { Id = created.Id, Name = "Producción 2", Url = "https://nueva.erp.pe", Server = "vps-1" }
            }, CancellationToken.None));
            Assert.True(update.Succeeded);
            Assert.True((await CheckAsync(created.LicenseKey)).Succeeded);

            var delete = await SendAsync(uow => new DeleteInstanceCommandHandler(uow, new TestLocalizer())
                .Handle(new DeleteInstanceCommand { Id = created.Id }, CancellationToken.None));
            Assert.True(delete.Succeeded);
            Assert.Equal(CheckLicenseCommandHandler.InvalidKeyMessage, (await CheckAsync(created.LicenseKey)).Messages.Single());
        }

        [Fact]
        public async Task A_customer_with_instances_cannot_be_deleted()
        {
            await CreateInstanceAsync();

            var result = await SendAsync(uow => new DeleteCustomerCommandHandler(uow, new TestLocalizer())
                .Handle(new DeleteCustomerCommand { Id = Alfa }, CancellationToken.None));

            Assert.Equal(DeleteCustomerCommandHandler.HasInstancesMessage, result.Messages.Single());
        }

        // ---------------------------------------------------------------- consulta de licencia
        [Fact]
        public async Task An_active_license_is_signed_echoes_the_nonce_and_carries_the_plan_limits()
        {
            await SubscribeAsync(Alfa, ErpBasic, SubscriptionStatus.Active);
            var created = await CreateInstanceAsync();

            var result = await CheckAsync(created.LicenseKey);

            Assert.True(result.Succeeded);
            var envelope = result.Data;
            var signature = Convert.FromBase64String(envelope.Signature);
            var bytes = Encoding.UTF8.GetBytes(envelope.Payload);
            Assert.True(Ed25519.Verify(signature, 0, Convert.FromBase64String(_signer.PublicKey), 0, bytes, 0, bytes.Length));

            var p = Payload(envelope);
            Assert.Equal(("Active", Nonce, "ERP-BASICO"), (p.GetProperty("status").GetString(), p.GetProperty("nonce").GetString(), p.GetProperty("plan").GetProperty("code").GetString()));
            Assert.Equal((2, 10, 4, true), (p.GetProperty("plan").GetProperty("maxCompanies").GetInt32(), p.GetProperty("plan").GetProperty("maxUsers").GetInt32(),
                p.GetProperty("plan").GetProperty("maxBranches").GetInt32(), p.GetProperty("plan").GetProperty("ai").GetBoolean()));
            Assert.Equal(Today.AddDays(30).ToString("yyyy-MM-dd"), p.GetProperty("coveredUntil").GetString());
            Assert.Equal("2026-11-01T17:00:00Z", p.GetProperty("issuedAt").GetString());
        }

        [Fact]
        public async Task A_suspended_subscription_yields_a_suspended_license_with_the_message()
        {
            await SubscribeAsync(Alfa, ErpBasic, SubscriptionStatus.Suspended, covered: Today.AddDays(-20));
            var created = await CreateInstanceAsync();

            var p = Payload((await CheckAsync(created.LicenseKey)).Data);

            Assert.Equal("Suspended", p.GetProperty("status").GetString());
            Assert.Equal(LicenseStateBuilder.SuspendedMessage, p.GetProperty("message").GetString());
        }

        [Fact]
        public async Task The_check_records_contact_and_usage_and_clamps_what_comes_from_outside()
        {
            await SubscribeAsync(Alfa, ErpBasic, SubscriptionStatus.Active);
            var created = await CreateInstanceAsync();

            Assert.True((await CheckAsync(created.LicenseKey, new LicenseCheckRequestDto
            {
                Nonce = Nonce, Version = new string('v', 80), LastMigration = "20261124_Phase12x", Companies = 2, Users = -5, Branches = 3, DocumentsThisMonth = 120
            })).Succeeded);

            await using var db = _database.CreateContext(_user);
            var i = await db.Set<ErpInstance>().AsNoTracking().SingleAsync();
            Assert.Equal(_clock.NowUtc, i.LastContactOnUtc);
            Assert.Equal(40, i.Version!.Length);
            Assert.Equal((2, (int?)null, 3, 120), (i.Companies, i.Users, i.Branches, i.DocumentsThisMonth));
        }

        [Fact]
        public async Task The_history_records_a_license_only_when_what_is_delivered_changes()
        {
            await SubscribeAsync(Alfa, ErpBasic, SubscriptionStatus.Active);
            var created = await CreateInstanceAsync();

            await CheckAsync(created.LicenseKey);
            await CheckAsync(created.LicenseKey);
            await CheckAsync(created.LicenseKey);
            await _database.SeedAsync(db => db.Set<Subscription>().Single().Status = SubscriptionStatus.Suspended);
            await CheckAsync(created.LicenseKey);

            await using var db = _database.CreateContext(_user);
            var issues = await db.Set<LicenseIssue>().AsNoTracking().OrderBy(x => x.Id).ToListAsync();
            Assert.Equal([LicenseStatus.Active, LicenseStatus.Suspended], issues.Select(x => x.Status).ToArray());
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("lic_desconocida")]
        public async Task An_unknown_or_missing_key_gets_the_same_generic_answer(string? key)
        {
            await CreateInstanceAsync();

            var result = await CheckAsync(key);

            Assert.False(result.Succeeded);
            Assert.Equal(CheckLicenseCommandHandler.InvalidKeyMessage, result.Messages.Single());
        }

        [Fact]
        public async Task Without_a_signing_key_no_license_is_delivered_and_nothing_is_recorded()
        {
            await SubscribeAsync(Alfa, ErpBasic, SubscriptionStatus.Active);
            var created = await CreateInstanceAsync();
            var off = new Ed25519LicenseSigner(Options.Create(new LicenseSettings()));

            var result = await SendAsync(uow => new CheckLicenseCommandHandler(uow, _clock, off).Handle(new CheckLicenseCommand
            {
                LicenseKey = created.LicenseKey, LicenseCheckRequestDto = new LicenseCheckRequestDto { Nonce = Nonce }
            }, CancellationToken.None));

            Assert.Equal(CheckLicenseCommandHandler.NotConfiguredMessage, result.Messages.Single());
            await using var db = _database.CreateContext(_user);
            Assert.Null((await db.Set<ErpInstance>().AsNoTracking().SingleAsync()).LastContactOnUtc);
        }

        [Theory]
        [InlineData(null, false)]
        [InlineData("", false)]
        [InlineData("corto", false)]
        [InlineData("con espacios 0123456789abcdef", false)]
        [InlineData("abcdefghijklmnop0123456789", true)]
        public void The_nonce_must_be_a_16_to_64_char_token(string? nonce, bool valid)
        {
            var result = new CheckLicenseCommandValidator().Validate(new CheckLicenseCommand
            {
                LicenseKey = "x", LicenseCheckRequestDto = new LicenseCheckRequestDto { Nonce = nonce }
            });
            Assert.Equal(valid, result.IsValid);
        }

        [Fact]
        public async Task The_list_shows_license_state_and_contact_for_each_instance()
        {
            await SubscribeAsync(Alfa, ErpBasic, SubscriptionStatus.Active);
            var alfa = await CreateInstanceAsync(Alfa);
            await CreateInstanceAsync(Beta); // Beta no tiene suscripción del ERP
            await CheckAsync(alfa.LicenseKey);

            var result = await SendAsync(uow => new GetAllInstancesPagedQueryHandler(uow, _clock, new TestLocalizer())
                .Handle(new GetAllInstancesPagedQuery(), CancellationToken.None));

            var rows = result.Data.ToDictionary(r => r.CustomerId);
            Assert.Equal(("Activa", "online"), (rows[Alfa].LicenseStatusName, rows[Alfa].ContactLevel));
            Assert.Equal(((int)LicenseStatus.Suspended, "never"), (rows[Beta].LicenseStatus, rows[Beta].ContactLevel));
            // Las que nunca se conectaron salen primero.
            Assert.Equal(Beta, result.Data.First().CustomerId);
        }
    }
}
