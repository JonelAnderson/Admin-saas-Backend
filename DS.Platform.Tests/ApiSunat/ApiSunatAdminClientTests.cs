using System.Net;
using System.Text;
using DS.Platform.Infrastructure.Shared.Services.ApiSunat;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace DS.Platform.Tests.ApiSunat
{
    /// <summary>
    /// El cliente HTTP de la API SUNAT: cabeceras de servicio, lectura del sobre <c>{ succeeded, messages, data }</c> y qué errores
    /// se reintentan. Nunca lanza.
    /// </summary>
    public class ApiSunatAdminClientTests
    {
        private const string Key = "k-0123456789abcdef0123456789abcdef";

        private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
        {
            public List<HttpRequestMessage> Requests { get; } = new();
            public string? LastBody { get; private set; }

            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                LastBody = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
                Requests.Add(request);
                return respond(request);
            }
        }

        private static HttpResponseMessage Json(HttpStatusCode status, string body)
            => new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

        private static string Tenant(long id = 7, bool active = true, bool billing = false)
            => $$"""
            {"succeeded":true,"messages":[],"data":{"id":{{id}},"ruc":"20131312955","razonSocial":"ALFA SAC","nombreComercial":"Alfa",
             "environment":"beta","plan":"pro","isActive":{{(active ? "true" : "false")}},"documentsThisMonth":12,"maxDocumentsMonth":1000,
             "billingSuspended":{{(billing ? "true" : "false")}}},"totalRecords":0,"errors":[]}
            """;

        private static (ApiSunatAdminClient Client, StubHandler Handler) Make(
            Func<HttpRequestMessage, HttpResponseMessage> respond, string? baseUrl = "http://api-sunat:8081", string? key = Key)
        {
            var handler = new StubHandler(respond);
            var client = new ApiSunatAdminClient(
                new HttpClient(handler),
                Options.Create(new ApiSunatSettings { BaseUrl = baseUrl, AdminKey = key }),
                NullLogger<ApiSunatAdminClient>.Instance);
            return (client, handler);
        }

        [Fact]
        public async Task It_sends_the_service_key_and_actor_and_reads_the_tenant()
        {
            var (client, handler) = Make(_ => Json(HttpStatusCode.OK, Tenant()));

            var result = await client.GetTenantAsync(7, CancellationToken.None);

            Assert.True(result.Success);
            Assert.Equal((7L, "20131312955", "ALFA SAC", "pro", true, false, 12, 1000),
                (result.Data!.Id, result.Data.Ruc, result.Data.BusinessName, result.Data.Plan, result.Data.IsActive,
                 result.Data.BillingSuspended, result.Data.DocumentsThisMonth, result.Data.MaxDocumentsMonth));
            var request = Assert.Single(handler.Requests);
            Assert.Equal("http://api-sunat:8081/api/admin/tenants/7", request.RequestUri!.ToString());
            Assert.Equal(Key, request.Headers.GetValues("X-Admin-Key").Single());
            Assert.Equal("sistema", request.Headers.GetValues("X-Admin-Actor").Single());
        }

        [Fact]
        public async Task Suspending_sends_a_payment_suspension_with_the_reason_and_a_clean_actor()
        {
            var (client, handler) = Make(_ => Json(HttpStatusCode.OK, Tenant(billing: true)));

            var result = await client.SetBillingSuspensionAsync(7, true, "Suscripción cancelada.", "cobran\r\nzas", CancellationToken.None);

            Assert.True(result.Success);
            Assert.True(result.Data!.BillingSuspended);
            var request = Assert.Single(handler.Requests);
            Assert.Equal(HttpMethod.Put, request.Method);
            Assert.EndsWith("/api/admin/tenants/7/estado", request.RequestUri!.AbsolutePath);
            Assert.Equal("cobranzas", request.Headers.GetValues("X-Admin-Actor").Single());
            Assert.Contains("\"activo\":false", handler.LastBody);
            Assert.Contains("\"tipo\":\"pago\"", handler.LastBody);
            Assert.Contains("\"motivo\":\"Suscripción cancelada.\"", handler.LastBody!.Replace("\\u00F3", "ó"));
        }

        [Fact]
        public async Task Reactivating_sends_no_reason()
        {
            var (client, handler) = Make(_ => Json(HttpStatusCode.OK, Tenant()));

            await client.SetBillingSuspensionAsync(7, false, "Suscripción vigente.", "cobranzas", CancellationToken.None);

            Assert.Contains("\"activo\":true", handler.LastBody);
            Assert.DoesNotContain("motivo", handler.LastBody);
        }

        [Theory]
        [InlineData(HttpStatusCode.Unauthorized)]
        [InlineData(HttpStatusCode.Forbidden)]
        [InlineData(HttpStatusCode.TooManyRequests)]
        [InlineData(HttpStatusCode.BadGateway)]
        [InlineData(HttpStatusCode.ServiceUnavailable)]
        public async Task Key_ip_rate_limit_and_server_errors_are_retryable(HttpStatusCode status)
        {
            var (client, _) = Make(_ => Json(status, """{"succeeded":false,"messages":["x"],"data":null,"totalRecords":0,"errors":[]}"""));

            var result = await client.GetTenantAsync(7, CancellationToken.None);

            Assert.False(result.Success);
            Assert.True(result.Retryable);
            Assert.Equal((int)status, result.StatusCode);
        }

        [Fact]
        public async Task A_business_error_is_not_retryable_and_a_missing_tenant_is_a_404()
        {
            var (client, _) = Make(_ => Json(HttpStatusCode.OK, """{"succeeded":false,"messages":["No existe la empresa."],"data":null,"totalRecords":0,"errors":[]}"""));

            var missing = await client.GetTenantAsync(7, CancellationToken.None);
            Assert.False(missing.Success);
            Assert.False(missing.Retryable);
            Assert.Equal(404, missing.StatusCode);

            var (other, _) = Make(_ => Json(HttpStatusCode.OK, """{"succeeded":false,"messages":[],"data":null,"totalRecords":0,"errors":[{"propertyName":"motivo","errorMessage":"El motivo es obligatorio."}]}"""));
            var invalid = await other.SetBillingSuspensionAsync(7, true, "", "a", CancellationToken.None);
            Assert.False(invalid.Retryable);
            Assert.Contains("El motivo es obligatorio.", invalid.Error);
        }

        [Fact]
        public async Task An_unreadable_answer_and_network_failures_never_throw()
        {
            var (garbage, _) = Make(_ => Json(HttpStatusCode.OK, "<html>nginx</html>"));
            var bad = await garbage.GetTenantAsync(7, CancellationToken.None);
            Assert.False(bad.Success);

            var (down, _) = Make(_ => throw new HttpRequestException("connection refused"));
            var offline = await down.GetTenantAsync(7, CancellationToken.None);
            Assert.False(offline.Success);
            Assert.True(offline.Retryable);
        }

        [Fact]
        public async Task A_redirect_is_never_followed_and_is_not_retried()
        {
            var (client, handler) = Make(_ => new HttpResponseMessage(HttpStatusCode.Redirect));

            var result = await client.GetTenantAsync(7, CancellationToken.None);

            Assert.False(result.Success);
            Assert.False(result.Retryable);
            Assert.Single(handler.Requests);
        }

        [Theory]
        [InlineData(null, Key)]
        [InlineData("", Key)]
        [InlineData("http://api-sunat:8081", null)]
        [InlineData("http://api-sunat:8081", "  ")]
        public async Task Without_address_or_key_it_sends_nothing_and_asks_to_retry_later(string? baseUrl, string? key)
        {
            var (client, handler) = Make(_ => Json(HttpStatusCode.OK, Tenant()), baseUrl, key);

            var result = await client.GetTenantAsync(7, CancellationToken.None);

            Assert.False(result.Success);
            Assert.True(result.Retryable);
            Assert.Equal(ApiSunatAdminClient.NotConfiguredMessage, result.Error);
            Assert.Empty(handler.Requests);
        }

        [Fact]
        public async Task Search_reads_a_page_of_tenants_and_escapes_the_text()
        {
            var body = """
            {"succeeded":true,"messages":[],"data":[{"id":2,"ruc":"20100070970","razonSocial":"BETA SAC","isActive":true},
             {"id":1,"ruc":"20131312955","razonSocial":"ALFA SAC","isActive":false}],"totalRecords":2,"errors":[]}
            """;
            var (client, handler) = Make(_ => Json(HttpStatusCode.OK, body));

            var result = await client.SearchTenantsAsync("a&b c", 1, 30, CancellationToken.None);

            Assert.True(result.Success);
            Assert.Equal(2, result.Data!.Total);
            Assert.Equal([(2L, true), (1L, false)], result.Data.Items.Select(t => (t.Id, t.IsActive)).ToArray());
            Assert.Contains("textFilter=a%26b%20c", handler.Requests.Single().RequestUri!.Query);
            Assert.Contains("records=30", handler.Requests.Single().RequestUri!.Query);
        }
    }
}
