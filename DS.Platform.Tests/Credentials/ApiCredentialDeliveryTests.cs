using System.Text.Json;
using DS.Platform.Application.DTOs.Credentials.Request;
using DS.Platform.Application.DTOs.Credentials.Response;
using DS.Platform.Application.Features.Credentials.Commands.Ack;
using DS.Platform.Application.Features.Credentials.Commands.Cancel;
using DS.Platform.Application.Features.Credentials.Commands.Create;
using DS.Platform.Application.Features.Credentials.Commands.Dispatch;
using DS.Platform.Application.Features.Credentials.Commands.Pull;
using DS.Platform.Application.Features.Credentials.Commands.Retry;
using DS.Platform.Application.Features.Credentials.Common;
using DS.Platform.Application.Features.Credentials.Queries.GetAllPaged;
using DS.Platform.Application.Features.Credentials.Validators;
using DS.Platform.Application.Features.Instances.Common;
using DS.Platform.Application.Features.Licensing.Commands.Check;
using DS.Platform.Application.Interfaces.Repositories;
using DS.Platform.Application.Interfaces.Services.ApiSunat;
using DS.Platform.Domain.Entities.ApiSunat;
using DS.Platform.Domain.Entities.Customers;
using DS.Platform.Domain.Entities.Erp;
using DS.Platform.Domain.Entities.Ops;
using DS.Platform.Domain.Enums;
using DS.Platform.Infrastructure.Contexts;
using DS.Platform.Infrastructure.Persistence.Contexts;
using DS.Platform.Shared.Commons.Bases.Request;
using DS.Platform.Shared.Wrapper;
using DS.Platform.Tests.Customers;
using DS.Platform.Tests.Deployments;
using DS.Platform.Tests.Support;
using Microsoft.EntityFrameworkCore;

namespace DS.Platform.Tests.Credentials
{
    internal sealed class FakeApiSunatSettings : IApiSunatSettings
    {
        public string? PublicUrl { get; set; } = "https://api.midominio.pe";
        public bool IsPublicUrlConfigured => PublicUrl is not null && PublicUrl.StartsWith("https://");
    }

    /// <summary>
    /// Entrega de las credenciales de la API SUNAT al ERP (fase C6b): pedirla solo guarda la bandeja, el despachador regenera las
    /// credenciales y las guarda cifradas, el ERP las recoge con su clave (solo las de su instancia) y las confirma, y el secreto se
    /// borra al confirmar, al vencer y al cancelar sin llegar nunca a un DTO de la pantalla, a un log ni a la auditoría.
    /// </summary>
    public class ApiCredentialDeliveryTests
    {
        private const int Alfa = 1, Beta = 2;
        private const int IssuerA1 = 1, IssuerA2 = 2, IssuerB1 = 3;
        private const int InstA = 100, InstA2 = 101, InstB = 200;
        private const string KeyA = "lic_alfa-0123456789abcdefghij", KeyA2 = "lic_alfa2-0123456789abcdefghij", KeyB = "lic_beta-0123456789abcdefghij";
        private const string Nonce = "abcdefghijklmnop0123456789";

        private readonly TestDatabase _database = new();
        private readonly TestCurrentUserService _user = TestCurrentUserService.User("soporte");
        private readonly TestClock _clock = new(new DateOnly(2026, 11, 24));
        private readonly FakeApiSunatAdminClient _api = new();
        private readonly FakeApiSunatSettings _settings = new();
        private readonly FakeProtector _protector = new();

        public ApiCredentialDeliveryTests()
        {
            _database.SeedAsync(db =>
            {
                db.Set<Customer>().AddRange(
                    new Customer { Id = Alfa, Ruc = CustomerHandlersTests.RucAlfa, BusinessName = "ALFA SAC" },
                    new Customer { Id = Beta, Ruc = CustomerHandlersTests.RucBeta, BusinessName = "BETA SAC" });
                db.Set<ApiSunatIssuer>().AddRange(
                    new ApiSunatIssuer { Id = IssuerA1, CustomerId = Alfa, TenantId = 501, Ruc = "20100000001", BusinessName = "ALFA UNO" },
                    new ApiSunatIssuer { Id = IssuerA2, CustomerId = Alfa, TenantId = 502, Ruc = "20100000002", BusinessName = "ALFA DOS" },
                    new ApiSunatIssuer { Id = IssuerB1, CustomerId = Beta, TenantId = 601, Ruc = "20100000003", BusinessName = "BETA UNO" });
                db.Set<ErpInstance>().AddRange(
                    new ErpInstance { Id = InstA, CustomerId = Alfa, Name = "Producción", LicenseKeyHash = LicenseKeys.Hash(KeyA), LicenseKeyHint = "ghij", IsActive = true },
                    new ErpInstance { Id = InstA2, CustomerId = Alfa, Name = "Pruebas", LicenseKeyHash = LicenseKeys.Hash(KeyA2), LicenseKeyHint = "ghij", IsActive = true },
                    new ErpInstance { Id = InstB, CustomerId = Beta, Name = "Beta prod", LicenseKeyHash = LicenseKeys.Hash(KeyB), LicenseKeyHint = "ghij", IsActive = true });
                db.Set<ErpInstance>().Local.ToList();
            }).GetAwaiter().GetResult();
            _api.With(FakeApiSunatAdminClient.Tenant(501, "20100000001", "ALFA UNO"))
                .With(FakeApiSunatAdminClient.Tenant(502, "20100000002", "ALFA DOS"))
                .With(FakeApiSunatAdminClient.Tenant(601, "20100000003", "BETA UNO"));
        }

        private async Task<T> SendAsync<T>(Func<IUnitOfWork<int>, Task<T>> send)
        {
            await using var db = _database.CreateContext(_user);
            return await send(HandlerKit.UnitOfWork(db, _user));
        }

        private Task<Result<int>> CreateAsync(int instance = InstA, int issuer = IssuerA1, bool confirm = true) => SendAsync(uow =>
            new CreateApiCredentialDeliveryCommandHandler(uow, _clock).Handle(new CreateApiCredentialDeliveryCommand
            {
                CreateApiCredentialDeliveryRequestDto = new CreateApiCredentialDeliveryRequestDto
                    { InstanceId = instance, IssuerId = issuer, ConfirmInvalidatePrevious = confirm }
            }, CancellationToken.None));

        private Task<Result<int>> DispatchAsync() => SendAsync(uow =>
            new DispatchApiCredentialDeliveriesCommandHandler(uow, _api, _settings, _protector, _clock)
                .Handle(new DispatchApiCredentialDeliveriesCommand(), CancellationToken.None));

        private Task<Result<ApiSunatPullResponseDto>> PullAsync(string? key = KeyA) => SendAsync(uow =>
            new PullApiSunatCredentialsCommandHandler(uow, _clock, _settings, _protector).Handle(new PullApiSunatCredentialsCommand
            {
                LicenseKey = key, ApiSunatPullRequestDto = new ApiSunatPullRequestDto { Nonce = Nonce }
            }, CancellationToken.None));

        private Task<Result<ApiSunatAckResponseDto>> AckAsync(string? key, params int[] ids) => SendAsync(uow =>
            new AckApiSunatCredentialsCommandHandler(uow, _clock).Handle(new AckApiSunatCredentialsCommand
            {
                LicenseKey = key, ApiSunatAckRequestDto = new ApiSunatAckRequestDto { Ids = ids.ToList() }
            }, CancellationToken.None));

        private Task<Result<int>> CancelAsync(int id) => SendAsync(uow =>
            new CancelApiCredentialDeliveryCommandHandler(uow, _clock).Handle(new CancelApiCredentialDeliveryCommand { Id = id }, CancellationToken.None));

        private Task<Result<int>> RetryAsync(int id) => SendAsync(uow =>
            new RetryApiCredentialDeliveryCommandHandler(uow, _clock).Handle(new RetryApiCredentialDeliveryCommand { Id = id }, CancellationToken.None));

        private async Task<ApiCredentialDelivery> DeliveryAsync(int id)
        {
            await using var db = _database.CreateContext(_user);
            return await db.Set<ApiCredentialDelivery>().AsNoTracking().SingleAsync(d => d.Id == id);
        }

        /// <summary>Pide y despacha: deja la entrega Lista con credenciales cifradas.</summary>
        private async Task<int> ReadyAsync(int instance = InstA, int issuer = IssuerA1)
        {
            var id = (await CreateAsync(instance, issuer)).Data;
            await DispatchAsync();
            return id;
        }

        private Task SetStatusAsync(int id, ApiCredentialDeliveryStatus status)
            => _database.SeedAsync(db => db.Set<ApiCredentialDelivery>().Single(d => d.Id == id).Status = status);

        // ------------------------------------------------------------------ reglas puras

        [Fact]
        public void Only_pending_sending_ready_and_failed_deliveries_are_open()
        {
            var open = Enum.GetValues<ApiCredentialDeliveryStatus>().Where(CredentialDeliveryRules.IsOpen).ToList();
            Assert.Equal(
                [ApiCredentialDeliveryStatus.Pending, ApiCredentialDeliveryStatus.Sending, ApiCredentialDeliveryStatus.Ready, ApiCredentialDeliveryStatus.Failed],
                open);
        }

        [Fact]
        public void The_ruc_is_exactly_eleven_digits_and_the_reason_names_only_the_instance()
        {
            Assert.True(CredentialDeliveryRules.IsValidRuc("20100000001"));
            Assert.False(CredentialDeliveryRules.IsValidRuc("2010000000"));
            Assert.False(CredentialDeliveryRules.IsValidRuc("2010000000A"));
            Assert.False(CredentialDeliveryRules.IsValidRuc(null));
            var reason = CredentialDeliveryRules.ReasonFor(7);
            Assert.Contains("7", reason);
            Assert.True(reason.Length <= 400);
        }

        [Fact]
        public void The_validators_require_ids_a_nonce_and_a_bounded_list_of_deliveries()
        {
            Assert.False(new CreateApiCredentialDeliveryCommandValidator().Validate(new CreateApiCredentialDeliveryCommand()).IsValid);
            Assert.False(new CreateApiCredentialDeliveryCommandValidator().Validate(new CreateApiCredentialDeliveryCommand
                { CreateApiCredentialDeliveryRequestDto = new() { InstanceId = 0, IssuerId = 1 } }).IsValid);

            var pull = new PullApiSunatCredentialsCommandValidator();
            Assert.False(pull.Validate(new PullApiSunatCredentialsCommand { ApiSunatPullRequestDto = new() { Nonce = "corto" } }).IsValid);
            Assert.False(pull.Validate(new PullApiSunatCredentialsCommand { ApiSunatPullRequestDto = new() { Nonce = new string('a', 65) } }).IsValid);
            Assert.False(pull.Validate(new PullApiSunatCredentialsCommand { ApiSunatPullRequestDto = new() { Nonce = "con espacios 123456789" } }).IsValid);
            Assert.True(pull.Validate(new PullApiSunatCredentialsCommand { ApiSunatPullRequestDto = new() { Nonce = Nonce } }).IsValid);

            var ack = new AckApiSunatCredentialsCommandValidator();
            Assert.False(ack.Validate(new AckApiSunatCredentialsCommand { ApiSunatAckRequestDto = new() { Ids = [] } }).IsValid);
            Assert.False(ack.Validate(new AckApiSunatCredentialsCommand { ApiSunatAckRequestDto = new() { Ids = [0] } }).IsValid);
            Assert.False(ack.Validate(new AckApiSunatCredentialsCommand
                { ApiSunatAckRequestDto = new() { Ids = Enumerable.Range(1, CredentialDeliveryRules.MaxPerAck + 1).ToList() } }).IsValid);
            Assert.True(ack.Validate(new AckApiSunatCredentialsCommand { ApiSunatAckRequestDto = new() { Ids = [1, 2] } }).IsValid);
        }

        // ------------------------------------------------------------------ pedir la entrega

        [Fact]
        public async Task Asking_only_saves_a_pending_delivery_and_never_calls_the_api()
        {
            var result = await CreateAsync();

            Assert.True(result.Succeeded, string.Join(" ", result.Messages));
            Assert.Empty(_api.RegenerateCalls);
            var delivery = await DeliveryAsync(result.Data);
            Assert.Equal((ApiCredentialDeliveryStatus.Pending, InstA, Alfa, IssuerA1, 501L, "20100000001"),
                (delivery.Status, delivery.InstanceId, delivery.CustomerId, delivery.IssuerId, delivery.TenantId, delivery.Ruc));
            Assert.Null(delivery.ApiKeyProtected);
            Assert.Null(delivery.ApiSecretProtected);
        }

        [Fact]
        public async Task The_operator_must_confirm_that_the_current_credentials_stop_working()
        {
            var result = await CreateAsync(confirm: false);

            Assert.False(result.Succeeded);
            Assert.Equal(CredentialDeliveryRules.ConfirmMessage, result.Messages.Single());
            await using var db = _database.CreateContext(_user);
            Assert.Empty(db.Set<ApiCredentialDelivery>());
        }

        [Fact]
        public async Task The_issuer_and_the_instance_must_belong_to_the_same_customer()
        {
            var result = await CreateAsync(instance: InstA, issuer: IssuerB1);

            Assert.Equal(CreateApiCredentialDeliveryCommandHandler.DifferentCustomerMessage, result.Messages.Single());
        }

        [Fact]
        public async Task Missing_inactive_or_deleted_instances_issuers_and_customers_are_rejected()
        {
            Assert.Equal(CreateApiCredentialDeliveryCommandHandler.InstanceNotFoundMessage, (await CreateAsync(instance: 999)).Messages.Single());
            Assert.Equal(CreateApiCredentialDeliveryCommandHandler.IssuerNotFoundMessage, (await CreateAsync(issuer: 999)).Messages.Single());

            await _database.SeedAsync(db => db.Set<ErpInstance>().Single(i => i.Id == InstA).IsActive = false);
            Assert.Equal(CreateApiCredentialDeliveryCommandHandler.InstanceInactiveMessage, (await CreateAsync()).Messages.Single());
            await _database.SeedAsync(db => db.Set<ErpInstance>().Single(i => i.Id == InstA).IsActive = true);

            await _database.SeedAsync(db => db.Set<ApiSunatIssuer>().Single(i => i.Id == IssuerA1).IsActive = false);
            Assert.Equal(CreateApiCredentialDeliveryCommandHandler.IssuerInactiveMessage, (await CreateAsync()).Messages.Single());
            await _database.SeedAsync(db => db.Set<ApiSunatIssuer>().Single(i => i.Id == IssuerA1).IsActive = true);

            await _database.SeedAsync(db => db.Set<Customer>().Single(c => c.Id == Alfa).IsActive = false);
            Assert.Equal(CreateApiCredentialDeliveryCommandHandler.CustomerInactiveMessage, (await CreateAsync()).Messages.Single());
        }

        [Fact]
        public async Task An_issuer_with_a_malformed_ruc_is_rejected()
        {
            await _database.SeedAsync(db => db.Set<ApiSunatIssuer>().Single(i => i.Id == IssuerA1).Ruc = "123");

            Assert.Equal(CreateApiCredentialDeliveryCommandHandler.InvalidRucMessage, (await CreateAsync()).Messages.Single());
        }

        [Fact]
        public async Task Only_one_delivery_can_be_open_per_issuer_and_a_closed_one_frees_it()
        {
            var first = (await CreateAsync()).Data;

            Assert.Equal(CreateApiCredentialDeliveryCommandHandler.OpenMessage, (await CreateAsync()).Messages.Single());
            // Otro emisor del mismo cliente sí.
            Assert.True((await CreateAsync(issuer: IssuerA2)).Succeeded);

            foreach (var closed in new[] { ApiCredentialDeliveryStatus.Delivered, ApiCredentialDeliveryStatus.Cancelled,
                                           ApiCredentialDeliveryStatus.Expired, ApiCredentialDeliveryStatus.Abandoned })
            {
                await SetStatusAsync(first, closed);
                var next = await CreateAsync();
                Assert.True(next.Succeeded, $"{closed}: {string.Join(" ", next.Messages)}");
                await SetStatusAsync(next.Data, ApiCredentialDeliveryStatus.Cancelled);
            }
        }

        // ------------------------------------------------------------------ despachador

        [Fact]
        public async Task The_dispatcher_regenerates_the_credentials_and_stores_them_encrypted()
        {
            var id = (await CreateAsync()).Data;

            Assert.Equal(1, (await DispatchAsync()).Data);

            var call = Assert.Single(_api.RegenerateCalls);
            Assert.Equal(501, call.TenantId);
            Assert.Equal(CredentialDeliveryRules.ReasonFor(InstA), call.Reason);
            Assert.Equal("soporte", call.Actor);

            var (key, secret) = _api.LastCredentials!.Value;
            var delivery = await DeliveryAsync(id);
            Assert.Equal(ApiCredentialDeliveryStatus.Ready, delivery.Status);
            Assert.NotEqual(key, delivery.ApiKeyProtected);
            Assert.NotEqual(secret, delivery.ApiSecretProtected);
            Assert.True(_protector.IsProtected(delivery.ApiKeyProtected));
            Assert.Equal(key, _protector.Unprotect(delivery.ApiKeyProtected!));
            Assert.Equal(secret, _protector.Unprotect(delivery.ApiSecretProtected!));
            Assert.Equal(_clock.NowUtc, delivery.ReadyOnUtc);
            Assert.Equal(_clock.NowUtc.AddDays(7), delivery.ExpiresOnUtc);
            Assert.Equal((1, null), (delivery.Attempts, delivery.LastError));
        }

        [Fact]
        public async Task A_ready_delivery_is_not_regenerated_again_by_the_next_pass()
        {
            await ReadyAsync();

            Assert.Equal(0, (await DispatchAsync()).Data);
            Assert.Single(_api.RegenerateCalls);
        }

        [Fact]
        public async Task A_retryable_failure_waits_and_keeps_no_secret_until_it_works()
        {
            _api.FailRegenerateWith = ApiSunatCallResult<ApiTenantCredentials>.Fail(503, "No se pudo conectar con la API SUNAT.", true);
            var id = (await CreateAsync()).Data;

            Assert.Equal(0, (await DispatchAsync()).Data);

            var failed = await DeliveryAsync(id);
            Assert.Equal((ApiCredentialDeliveryStatus.Failed, 1, "No se pudo conectar con la API SUNAT."), (failed.Status, failed.Attempts, failed.LastError));
            Assert.Equal(_clock.NowUtc.AddMinutes(1), failed.NextAttemptOnUtc);
            Assert.Null(failed.ApiKeyProtected);

            // Aún no toca; cuando toca y la API vuelve, queda lista.
            Assert.Equal(0, (await DispatchAsync()).Data);
            Assert.Single(_api.RegenerateCalls);
            _api.FailRegenerateWith = null;
            _clock.Advance(TimeSpan.FromMinutes(2));
            Assert.Equal(1, (await DispatchAsync()).Data);
            Assert.Equal(ApiCredentialDeliveryStatus.Ready, (await DeliveryAsync(id)).Status);
        }

        [Fact]
        public async Task A_rejected_request_is_abandoned_and_a_retry_starts_over()
        {
            _api.FailRegenerateWith = ApiSunatCallResult<ApiTenantCredentials>.Fail(404, "No existe la empresa.", false);
            var id = (await CreateAsync()).Data;
            await DispatchAsync();
            Assert.Equal(ApiCredentialDeliveryStatus.Abandoned, (await DeliveryAsync(id)).Status);

            _api.FailRegenerateWith = null;
            Assert.True((await RetryAsync(id)).Succeeded);
            var pending = await DeliveryAsync(id);
            Assert.Equal((ApiCredentialDeliveryStatus.Pending, 0), (pending.Status, pending.Attempts));
            Assert.Equal(1, (await DispatchAsync()).Data);
            Assert.Equal(ApiCredentialDeliveryStatus.Ready, (await DeliveryAsync(id)).Status);
        }

        [Fact]
        public async Task Retrying_only_applies_to_failed_or_abandoned_deliveries_and_not_over_another_open_one()
        {
            var id = (await CreateAsync()).Data;
            Assert.Equal(RetryApiCredentialDeliveryCommandHandler.NotRetryableMessage, (await RetryAsync(id)).Messages.Single());
            Assert.Equal(RetryApiCredentialDeliveryCommandHandler.NotFoundMessage, (await RetryAsync(999)).Messages.Single());

            await SetStatusAsync(id, ApiCredentialDeliveryStatus.Abandoned);
            Assert.True((await CreateAsync()).Succeeded);   // otra entrega abierta del mismo emisor
            Assert.Equal(RetryApiCredentialDeliveryCommandHandler.OpenMessage, (await RetryAsync(id)).Messages.Single());
        }

        [Fact]
        public async Task Without_the_public_url_nothing_is_regenerated_so_the_current_credentials_keep_working()
        {
            _settings.PublicUrl = null;
            var id = (await CreateAsync()).Data;

            await DispatchAsync();

            Assert.Empty(_api.RegenerateCalls);
            var delivery = await DeliveryAsync(id);
            Assert.Equal((ApiCredentialDeliveryStatus.Failed, CredentialDeliveryRules.NotConfiguredMessage), (delivery.Status, delivery.LastError));
        }

        [Fact]
        public async Task A_delivery_being_sent_is_taken_once_and_retaken_when_its_lease_expires()
        {
            var id = (await CreateAsync()).Data;
            await _database.SeedAsync(db =>
            {
                var d = db.Set<ApiCredentialDelivery>().Single();
                d.Status = ApiCredentialDeliveryStatus.Sending;
                d.LockedUntilUtc = _clock.NowUtc.AddMinutes(3);
            });

            Assert.Equal(0, (await DispatchAsync()).Data);
            Assert.Empty(_api.RegenerateCalls);
            Assert.Equal(CancelApiCredentialDeliveryCommandHandler.BusyMessage, (await CancelAsync(id)).Messages.Single());

            _clock.Advance(TimeSpan.FromMinutes(6));
            Assert.Equal(1, (await DispatchAsync()).Data);
            Assert.Single(_api.RegenerateCalls);
        }

        // ------------------------------------------------------------------ el ERP recoge

        [Fact]
        public async Task The_erp_pulls_its_ready_credentials_with_the_public_url_of_the_api()
        {
            var id = await ReadyAsync();
            var (key, secret) = _api.LastCredentials!.Value;

            var pull = await PullAsync();

            Assert.True(pull.Succeeded);
            Assert.Equal("https://api.midominio.pe", pull.Data.BaseUrl);
            var delivery = Assert.Single(pull.Data.Deliveries);
            Assert.Equal((id, "20100000001", key, secret), (delivery.Id, delivery.Ruc, delivery.ApiKey, delivery.ApiSecret));
            // Sigue guardada (cifrada) hasta que el ERP confirme, y anota cuándo la vieron.
            var stored = await DeliveryAsync(id);
            Assert.Equal((ApiCredentialDeliveryStatus.Ready, _clock.NowUtc), (stored.Status, stored.LastPulledOnUtc));
            Assert.NotNull(stored.ApiSecretProtected);
        }

        [Fact]
        public async Task An_instance_only_sees_its_own_deliveries_never_another_instance_of_the_same_or_other_customer()
        {
            await ReadyAsync(InstA, IssuerA1);
            await ReadyAsync(InstA2, IssuerA2);
            await ReadyAsync(InstB, IssuerB1);

            var a = (await PullAsync(KeyA)).Data.Deliveries.Single();
            var a2 = (await PullAsync(KeyA2)).Data.Deliveries.Single();
            var b = (await PullAsync(KeyB)).Data.Deliveries.Single();

            Assert.Equal(["20100000001", "20100000002", "20100000003"], new[] { a.Ruc, a2.Ruc, b.Ruc });
            Assert.Equal(3, new[] { a.ApiKey, a2.ApiKey, b.ApiKey }.Distinct().Count());
        }

        [Fact]
        public async Task An_unknown_empty_or_inactive_key_gets_the_same_generic_message_and_nothing_else()
        {
            await ReadyAsync();
            await _database.SeedAsync(db => db.Set<ErpInstance>().Single(i => i.Id == InstB).IsActive = false);

            foreach (var key in new[] { "lic_desconocida", "", null, "   ", new string('x', 101), KeyB })
            {
                var pull = await PullAsync(key);
                Assert.False(pull.Succeeded);
                Assert.Equal(CheckLicenseCommandHandler.InvalidKeyMessage, pull.Messages.Single());
                Assert.Null(pull.Data);
            }
            var ack = await AckAsync("lic_desconocida", 1);
            Assert.Equal(CheckLicenseCommandHandler.InvalidKeyMessage, ack.Messages.Single());
        }

        [Fact]
        public async Task Without_the_public_url_the_erp_gets_an_empty_list_and_the_delivery_is_not_marked_as_seen()
        {
            var id = await ReadyAsync();
            _settings.PublicUrl = null;

            var pull = await PullAsync();

            Assert.True(pull.Succeeded);
            Assert.Null(pull.Data.BaseUrl);
            Assert.Empty(pull.Data.Deliveries);
            Assert.Null((await DeliveryAsync(id)).LastPulledOnUtc);
        }

        [Fact]
        public async Task Only_ready_and_unexpired_deliveries_are_pulled_and_at_most_twenty()
        {
            await _database.SeedAsync(db =>
            {
                for (var i = 0; i < 25; i++)
                {
                    db.Set<ApiCredentialDelivery>().Add(new ApiCredentialDelivery
                    {
                        InstanceId = InstA, CustomerId = Alfa, IssuerId = IssuerA1, TenantId = 501, Ruc = "20100000001",
                        Status = ApiCredentialDeliveryStatus.Ready, ReadyOnUtc = _clock.NowUtc, ExpiresOnUtc = _clock.NowUtc.AddDays(7),
                        ApiKeyProtected = _protector.Protect($"k{i}"), ApiSecretProtected = _protector.Protect($"s{i}")
                    });
                }
                foreach (var status in new[] { ApiCredentialDeliveryStatus.Pending, ApiCredentialDeliveryStatus.Delivered, ApiCredentialDeliveryStatus.Cancelled })
                {
                    db.Set<ApiCredentialDelivery>().Add(new ApiCredentialDelivery
                    {
                        InstanceId = InstA, CustomerId = Alfa, IssuerId = IssuerA2, TenantId = 502, Ruc = "20100000002", Status = status
                    });
                }
                db.Set<ApiCredentialDelivery>().Add(new ApiCredentialDelivery
                {
                    InstanceId = InstA, CustomerId = Alfa, IssuerId = IssuerA2, TenantId = 502, Ruc = "20100000002",
                    Status = ApiCredentialDeliveryStatus.Ready, ExpiresOnUtc = _clock.NowUtc.AddMinutes(-1),
                    ApiKeyProtected = _protector.Protect("viejo"), ApiSecretProtected = _protector.Protect("viejo")
                });
            });

            var pull = await PullAsync();

            Assert.Equal(CredentialDeliveryRules.MaxPerPull, pull.Data.Deliveries.Count);
            Assert.DoesNotContain(pull.Data.Deliveries, d => d.ApiKey == "viejo");
            Assert.All(pull.Data.Deliveries, d => Assert.Equal("20100000001", d.Ruc));
        }

        [Fact]
        public async Task The_pulled_response_never_prints_the_secret()
        {
            await ReadyAsync();
            var secret = _api.LastCredentials!.Value.ApiSecret;

            var pull = await PullAsync();

            Assert.DoesNotContain(secret, pull.Data.ToString());
            Assert.DoesNotContain(secret, pull.Data.Deliveries.Single().ToString());
            Assert.DoesNotContain(secret, new ApiTenantCredentials("k", secret).ToString());
        }

        // ------------------------------------------------------------------ el ERP confirma

        [Fact]
        public async Task Acknowledging_marks_it_delivered_and_wipes_the_secret()
        {
            var id = await ReadyAsync();
            await PullAsync();

            var ack = await AckAsync(KeyA, id);

            Assert.Equal(1, ack.Data.Acknowledged);
            var delivery = await DeliveryAsync(id);
            Assert.Equal((ApiCredentialDeliveryStatus.Delivered, _clock.NowUtc), (delivery.Status, delivery.DeliveredOnUtc));
            Assert.Null(delivery.ApiKeyProtected);
            Assert.Null(delivery.ApiSecretProtected);
            Assert.Empty((await PullAsync()).Data.Deliveries);
        }

        [Fact]
        public async Task Acknowledging_is_idempotent_and_ignores_ids_of_other_instances_without_touching_them()
        {
            var mine = await ReadyAsync(InstA, IssuerA1);
            var other = await ReadyAsync(InstA2, IssuerA2);

            Assert.Equal(1, (await AckAsync(KeyA, mine, other)).Data.Acknowledged);
            Assert.Equal(1, (await AckAsync(KeyA, mine)).Data.Acknowledged);
            Assert.Equal(0, (await AckAsync(KeyA, 999)).Data.Acknowledged);

            var untouched = await DeliveryAsync(other);
            Assert.Equal(ApiCredentialDeliveryStatus.Ready, untouched.Status);
            Assert.NotNull(untouched.ApiSecretProtected);
        }

        [Fact]
        public async Task A_cancelled_or_expired_delivery_cannot_be_acknowledged()
        {
            var a = await ReadyAsync(InstA, IssuerA1);
            var b = await ReadyAsync(InstA, IssuerA2);
            await CancelAsync(a);
            _clock.Advance(TimeSpan.FromDays(8));
            await DispatchAsync();

            Assert.Equal(0, (await AckAsync(KeyA, a, b)).Data.Acknowledged);
            Assert.Equal(ApiCredentialDeliveryStatus.Cancelled, (await DeliveryAsync(a)).Status);
            Assert.Equal(ApiCredentialDeliveryStatus.Expired, (await DeliveryAsync(b)).Status);
        }

        // ------------------------------------------------------------------ vencer y cancelar

        [Fact]
        public async Task A_ready_delivery_nobody_picks_up_in_seven_days_expires_and_loses_its_secret()
        {
            var id = await ReadyAsync();

            _clock.Advance(TimeSpan.FromDays(6));
            await DispatchAsync();
            Assert.Equal(ApiCredentialDeliveryStatus.Ready, (await DeliveryAsync(id)).Status);

            _clock.Advance(TimeSpan.FromDays(1).Add(TimeSpan.FromMinutes(1)));
            await DispatchAsync();

            var delivery = await DeliveryAsync(id);
            Assert.Equal((ApiCredentialDeliveryStatus.Expired, DispatchApiCredentialDeliveriesCommandHandler.ExpiredMessage), (delivery.Status, delivery.LastError));
            Assert.Null(delivery.ApiKeyProtected);
            Assert.Null(delivery.ApiSecretProtected);
            Assert.Empty((await PullAsync()).Data.Deliveries);
            // Hay que entregar de nuevo, y eso regenera otra vez.
            Assert.True((await CreateAsync()).Succeeded);
        }

        [Fact]
        public async Task Cancelling_wipes_the_secret_and_warns_when_the_api_had_already_regenerated()
        {
            var ready = await ReadyAsync();
            var pending = (await CreateAsync(issuer: IssuerA2)).Data;

            var plain = await CancelAsync(pending);
            var warned = await CancelAsync(ready);

            Assert.Equal(CancelApiCredentialDeliveryCommandHandler.DoneMessage, plain.Messages.Single());
            Assert.Equal(CancelApiCredentialDeliveryCommandHandler.DoneReadyMessage, warned.Messages.Single());
            var delivery = await DeliveryAsync(ready);
            Assert.Equal(ApiCredentialDeliveryStatus.Cancelled, delivery.Status);
            Assert.Null(delivery.ApiKeyProtected);
            Assert.Null(delivery.ApiSecretProtected);
            Assert.Empty((await PullAsync()).Data.Deliveries);
        }

        [Fact]
        public async Task A_delivered_or_missing_delivery_cannot_be_cancelled()
        {
            var id = await ReadyAsync();
            await AckAsync(KeyA, id);

            Assert.Equal(CancelApiCredentialDeliveryCommandHandler.NotCancellableMessage, (await CancelAsync(id)).Messages.Single());
            Assert.Equal(CancelApiCredentialDeliveryCommandHandler.NotFoundMessage, (await CancelAsync(999)).Messages.Single());
        }

        // ------------------------------------------------------------------ nada secreto sale

        [Fact]
        public async Task The_listing_filters_and_shows_the_state_but_never_carries_a_secret()
        {
            var ready = await ReadyAsync(InstA, IssuerA1);
            await CreateAsync(InstB, IssuerB1);
            var (key, secret) = _api.LastCredentials!.Value;

            var all = await SendAsync(uow => new GetAllApiCredentialDeliveriesPagedQueryHandler(uow, new TestLocalizer())
                .Handle(new GetAllApiCredentialDeliveriesPagedQuery(), CancellationToken.None));
            var onlyReady = await SendAsync(uow => new GetAllApiCredentialDeliveriesPagedQueryHandler(uow, new TestLocalizer())
                .Handle(new GetAllApiCredentialDeliveriesPagedQuery { Status = ApiCredentialDeliveryStatus.Ready }, CancellationToken.None));
            var byText = await SendAsync(uow => new GetAllApiCredentialDeliveriesPagedQueryHandler(uow, new TestLocalizer())
                .Handle(new GetAllApiCredentialDeliveriesPagedQuery { BaseFiltersRequest = new BaseFiltersRequest { TextFilter = "beta" } }, CancellationToken.None));

            Assert.Equal(2, all.TotalRecords);
            var row = Assert.Single(onlyReady.Data);
            Assert.Equal((ready, "ALFA SAC", "Producción", "20100000001", "ALFA UNO", true), (row.Id, row.CustomerName, row.InstanceName, row.IssuerRuc, row.IssuerName, row.WaitingForErp));
            Assert.Equal("Lista (esperando al ERP)", row.StatusName);
            Assert.Equal(1, byText.TotalRecords);

            var json = JsonSerializer.Serialize(all.Data);
            Assert.DoesNotContain(secret, json);
            Assert.DoesNotContain(key, json);
            Assert.DoesNotContain(Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(secret)), json);
            Assert.DoesNotContain(typeof(ApiCredentialDeliveryResponseDto).GetProperties(),
                p => p.Name.Contains("Secret", StringComparison.OrdinalIgnoreCase) || p.Name.Contains("ApiKey", StringComparison.OrdinalIgnoreCase)
                     || p.Name.Contains("Protected", StringComparison.OrdinalIgnoreCase));
        }

        [Fact]
        public async Task The_waiting_for_erp_flag_clears_once_the_erp_has_seen_it()
        {
            await ReadyAsync();
            await PullAsync();

            var list = await SendAsync(uow => new GetAllApiCredentialDeliveriesPagedQueryHandler(uow, new TestLocalizer())
                .Handle(new GetAllApiCredentialDeliveriesPagedQuery(), CancellationToken.None));

            Assert.False(list.Data.Single().WaitingForErp);
            Assert.NotNull(list.Data.Single().LastPulledOnUtc);
        }

        [Fact]
        public async Task The_secret_never_reaches_the_audit_trail_or_a_last_error()
        {
            var id = await ReadyAsync();
            await PullAsync();
            await AckAsync(KeyA, id);
            var (key, secret) = _api.LastCredentials!.Value;

            await using var db = _database.CreateContext(_user);
            var audits = await db.AuditTrails.AsNoTracking().ToListAsync();
            Assert.Contains(audits, a => a.TableName == nameof(ApiCredentialDelivery));
            var protectedKey = _protector.Protect(key);
            var protectedSecret = _protector.Protect(secret);
            Assert.DoesNotContain(audits, a => Leaks(a.NewValues, key, secret, protectedKey, protectedSecret)
                                               || Leaks(a.OldValues, key, secret, protectedKey, protectedSecret));
            var delivery = await DeliveryAsync(id);
            Assert.DoesNotContain(secret, delivery.LastError ?? string.Empty);
        }

        private static bool Leaks(string? json, params string[] values) => values.Any(v => (json ?? string.Empty).Contains(v));

        [Fact]
        public void The_audit_masks_the_key_and_the_secret_of_a_delivery_only()
        {
            var delivery = new ApiCredentialDelivery { InstanceId = 1, CustomerId = 1, IssuerId = 1, TenantId = 1, Ruc = "20100000001" };
            Assert.True(AuditableContext.IsSensitiveColumn(delivery, nameof(ApiCredentialDelivery.ApiKeyProtected)));
            Assert.True(AuditableContext.IsSensitiveColumn(delivery, nameof(ApiCredentialDelivery.ApiSecretProtected)));
            Assert.False(AuditableContext.IsSensitiveColumn(delivery, nameof(ApiCredentialDelivery.Ruc)));
            Assert.False(AuditableContext.IsSensitiveColumn(delivery, nameof(ApiCredentialDelivery.Status)));
        }
    }
}
