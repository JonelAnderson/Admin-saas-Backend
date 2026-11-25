using DS.Platform.Application.Features.Dashboard.Queries.Get;
using DS.Platform.Application.Interfaces.Repositories;
using DS.Platform.Domain.Entities.Billing;
using DS.Platform.Domain.Entities.Customers;
using DS.Platform.Domain.Entities.Erp;
using DS.Platform.Domain.Entities.Ops;
using DS.Platform.Domain.Enums;
using DS.Platform.Tests.Customers;
using DS.Platform.Tests.Support;

namespace DS.Platform.Tests.Notifications
{
    /// <summary>Tablero de inicio: cuenta bien lo que pide atención y no falla si la API SUNAT no responde.</summary>
    public class DashboardTests
    {
        private static readonly DateOnly Today = new(2026, 11, 18);   // miércoles; el mes de Lima es noviembre

        private readonly TestDatabase _database = new();
        private readonly TestCurrentUserService _user = TestCurrentUserService.User("soporte");
        private readonly TestClock _clock = new(Today);
        private readonly FakeApiSunatAdminClient _api = new();

        private Task<DS.Platform.Shared.Wrapper.Result<DS.Platform.Application.DTOs.Dashboard.Response.DashboardResponseDto>> GetAsync()
            => SendAsync(uow => new GetDashboardQueryHandler(uow, _api, _clock).Handle(new GetDashboardQuery(), CancellationToken.None));

        private async Task<T> SendAsync<T>(Func<IUnitOfWork<int>, Task<T>> send)
        {
            await using var db = _database.CreateContext(_user);
            return await send(HandlerKit.UnitOfWork(db, _user));
        }

        private Task SeedAsync() => _database.SeedAsync(db =>
        {
            db.Set<Customer>().AddRange(
                new Customer { Id = 1, Ruc = CustomerHandlersTests.RucAlfa, BusinessName = "ALFA SAC" },
                new Customer { Id = 2, Ruc = CustomerHandlersTests.RucBeta, BusinessName = "BETA SAC" });
            db.Set<Plan>().Add(new Plan { Id = 10, Product = PlanProduct.Erp, Code = "ERP", Name = "ERP Básico", MonthlyPrice = 350m });
            db.Set<Subscription>().AddRange(
                // vence en 3 días → "esta semana"
                new Subscription { Id = 1, CustomerId = 1, PlanId = 10, Product = PlanProduct.Erp, Status = SubscriptionStatus.Active, CoveredUntil = Today.AddDays(3), IsActive = true },
                // vence en 20 días → no sale
                new Subscription { Id = 2, CustomerId = 2, PlanId = 10, Product = PlanProduct.Erp, Status = SubscriptionStatus.Active, CoveredUntil = Today.AddDays(20), IsActive = true },
                // en gracia y se suspende en 2 días → sale como "Se suspende"
                new Subscription { Id = 3, CustomerId = 2, PlanId = 10, Product = PlanProduct.ApiSunat, Status = SubscriptionStatus.Grace, CoveredUntil = Today.AddDays(-4), SuspendOn = Today.AddDays(2), IsActive = true },
                new Subscription { Id = 4, CustomerId = 1, PlanId = 10, Product = PlanProduct.ApiSunat, Status = SubscriptionStatus.Suspended, CoveredUntil = Today.AddDays(-40), IsActive = true });
            db.Set<Payment>().AddRange(
                new Payment { Id = 1, SubscriptionId = 1, CustomerId = 1, PaidOn = new DateOnly(2026, 11, 2), Amount = 350m, Currency = "PEN" },
                new Payment { Id = 2, SubscriptionId = 2, CustomerId = 2, PaidOn = new DateOnly(2026, 11, 9), Amount = 100m, Currency = "PEN" },
                new Payment { Id = 3, SubscriptionId = 2, CustomerId = 2, PaidOn = new DateOnly(2026, 11, 10), Amount = 50m, Currency = "USD" },
                // anulado y de otro mes: no cuentan
                new Payment { Id = 4, SubscriptionId = 2, CustomerId = 2, PaidOn = new DateOnly(2026, 11, 11), Amount = 999m, Currency = "PEN", Status = PaymentStatus.Voided },
                new Payment { Id = 5, SubscriptionId = 2, CustomerId = 2, PaidOn = new DateOnly(2026, 10, 30), Amount = 999m, Currency = "PEN" });
            var now = DateTime.SpecifyKind(Today.ToDateTime(new TimeOnly(17, 0)), DateTimeKind.Utc);
            db.Set<ErpInstance>().AddRange(
                new ErpInstance { Id = 1, CustomerId = 1, Name = "Producción", LicenseKeyHash = "a", LicenseKeyHint = "1111", Version = "1.5.0", LastContactOnUtc = now.AddMinutes(-5) },
                new ErpInstance { Id = 2, CustomerId = 2, Name = "Producción", LicenseKeyHash = "b", LicenseKeyHint = "2222", Version = "1.4.2+abc", LastContactOnUtc = now.AddDays(-3) },
                new ErpInstance { Id = 3, CustomerId = 2, Name = "Pruebas", LicenseKeyHash = "c", LicenseKeyHint = "3333" });
            db.Set<Notification>().Add(new Notification
            {
                Id = 1, Type = NotificationType.DueSoon, CustomerId = 1, DedupKey = "k1", Subject = "s", Body = "b", Status = NotificationStatus.Failed
            });
        });

        [Fact]
        public async Task The_dashboard_counts_subscriptions_revenue_and_the_weeks_due_dates()
        {
            await SeedAsync();

            var d = (await GetAsync()).Data;

            Assert.Equal(("2026-11-18", 2), (d.Today, d.Customers));
            Assert.Equal([0, 2, 1, 1, 0], d.SubscriptionsByStatus.Select(s => s.Count).ToArray());   // prueba, activas, gracia, suspendidas, canceladas
            Assert.Equal(2, d.SubscriptionsByStatus.Single(s => s.Status == (int)SubscriptionStatus.Active).Count);
            Assert.Equal(1, d.SubscriptionsByStatus.Single(s => s.Status == (int)SubscriptionStatus.Grace).Count);
            Assert.Equal(1, d.SubscriptionsByStatus.Single(s => s.Status == (int)SubscriptionStatus.Suspended).Count);

            Assert.Equal([("PEN", 450m, 2), ("USD", 50m, 1)], d.MonthRevenue.Select(r => (r.Currency, r.Amount, r.Payments)).ToArray());

            // Las más cercanas primero.
            Assert.Equal(new[] { ("BETA SAC", "Se suspende", "2026-11-20", 2), ("ALFA SAC", "Vence", "2026-11-21", 3) },
                d.DueThisWeek.Select(x => (x.CustomerName, x.Event, x.Date, x.DaysLeft)).ToArray());
        }

        [Fact]
        public async Task The_dashboard_lists_lost_and_outdated_instances_and_failed_notices()
        {
            await SeedAsync();

            var d = (await GetAsync()).Data;

            Assert.Equal(3, d.InstancesTotal);
            Assert.Equal("1.5.0", d.LatestVersion);
            // Sin contacto: la que lleva 3 días y la que nunca se conectó (la más vieja primero).
            Assert.Equal([3, 2], d.InstancesWithoutContact.Select(i => i.Id).ToArray());
            // Desactualizada: solo la que informa 1.4.2 (la que no informa versión no se compara).
            Assert.Equal([2], d.InstancesOutdated.Select(i => i.Id).ToArray());
            Assert.Equal(1, d.NotificationsNeedingAttention);
            Assert.Equal(0, d.RemoteActionsNeedingAttention);
        }

        [Fact]
        public async Task The_api_summary_is_included_and_a_down_api_does_not_fail_the_dashboard()
        {
            await SeedAsync();
            _api.Dashboard = new(5, 100, 7, 2, 1, 3, 4);
            _api.RejectionsToday = 6;

            var ok = (await GetAsync()).Data.Api!;
            Assert.True(ok.Available);
            Assert.Equal((5L, 7L, 6L, 3L, 4L), (ok.TenantsActive, ok.RejectedThisMonth, ok.RejectedToday ?? -1, ok.CertificatesExpiring, ok.CertificatesBroken));

            _api.Down = true;
            var result = await GetAsync();
            Assert.True(result.Succeeded);
            Assert.False(result.Data.Api!.Available);
            Assert.False(string.IsNullOrWhiteSpace(result.Data.Api.Error));
            Assert.Equal(2, result.Data.Customers);
        }

        [Theory]
        [InlineData("1.4.0", "1.4.0")]
        [InlineData("v1.4.0+abc123", "1.4.0")]
        [InlineData("2.0.1-beta", "2.0.1")]
        [InlineData("basura", null)]
        [InlineData(null, null)]
        public void Versions_compare_ignoring_build_metadata(string? text, string? expected)
            => Assert.Equal(expected, GetDashboardQueryHandler.ParseVersion(text)?.ToString());
    }
}
