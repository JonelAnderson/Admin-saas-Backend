using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using DS.Platform.Application.Interfaces.Services.ApiSunat;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DS.Platform.Infrastructure.Shared.Services.ApiSunat
{
    /// <summary>Sección <c>ApiSunat</c>: la dirección va en appsettings; <c>AdminKey</c> solo en user-secrets / variable de entorno.</summary>
    public class ApiSunatSettings
    {
        public const string SectionName = "ApiSunat";

        /// <summary>Dirección base de la API SUNAT (p. ej. http://erp-sunat-api:8081). Vacía = integración apagada.</summary>
        public string? BaseUrl { get; set; }

        /// <summary>Clave de servicio (<c>ADMIN_SERVICE_KEY</c> de la API). Nunca se registra en el log.</summary>
        public string? AdminKey { get; set; }

        /// <summary>
        /// Dirección **pública** (https) de la API SUNAT: la que la consola entrega al ERP junto con las credenciales de su empresa
        /// (fase C6b). Distinta de <see cref="BaseUrl"/>, que puede ser interna. Vacía = no se entregan credenciales. No es secreta.
        /// </summary>
        public string? PublicUrl { get; set; }

        public int TimeoutSeconds { get; set; } = 20;
    }

    /// <summary>
    /// Cliente de <c>/api/admin/**</c> de la API SUNAT con la clave de servicio (<c>X-Admin-Key</c> + <c>X-Admin-Actor</c>). Nunca lanza:
    /// todo problema vuelve como <see cref="ApiSunatCallResult{T}"/> con <c>Retryable</c> (red, tiempo agotado, 429, 5xx, 401 / 403
    /// por clave o IP aún sin configurar) o sin él (petición rechazada por la API, empresa inexistente). No sigue redirecciones: la
    /// clave no puede viajar a otra dirección. El sobre de la API es <c>{ succeeded, messages, data, totalRecords }</c> y un error
    /// de negocio responde HTTP 200 con <c>succeeded: false</c>.
    /// </summary>
    public class ApiSunatAdminClient : IApiSunatAdminClient
    {
        public const string NotConfiguredMessage = "La integración con la API SUNAT no está configurada (ApiSunat:BaseUrl y ApiSunat:AdminKey).";

        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

        private readonly HttpClient _http;
        private readonly ApiSunatSettings _settings;
        private readonly ILogger<ApiSunatAdminClient> _logger;

        public ApiSunatAdminClient(HttpClient http, IOptions<ApiSunatSettings> settings, ILogger<ApiSunatAdminClient> logger)
        {
            _http = http;
            _settings = settings.Value;
            _logger = logger;
        }

        public bool IsConfigured => Uri.TryCreate(_settings.BaseUrl, UriKind.Absolute, out _) && !string.IsNullOrWhiteSpace(_settings.AdminKey);

        public async Task<ApiSunatCallResult<ApiTenantInfo>> GetTenantAsync(long tenantId, CancellationToken cancellationToken)
        {
            var call = await SendAsync(HttpMethod.Get, $"api/admin/tenants/{tenantId}", null, null, cancellationToken);
            return ReadTenant(call);
        }

        public async Task<ApiSunatCallResult<ApiTenantPage>> SearchTenantsAsync(string? text, int page, int pageSize, CancellationToken cancellationToken)
        {
            var query = new StringBuilder($"api/admin/tenants?records={Math.Clamp(pageSize, 1, 50)}&numPage={Math.Max(page, 1)}");
            if (!string.IsNullOrWhiteSpace(text))
            {
                query.Append("&textFilter=").Append(Uri.EscapeDataString(text.Trim()));
            }
            var call = await SendAsync(HttpMethod.Get, query.ToString(), null, null, cancellationToken);
            if (!call.Success)
            {
                return ApiSunatCallResult<ApiTenantPage>.Fail(call.StatusCode, call.Error!, call.Retryable);
            }

            var rows = call.Data!.Data is { ValueKind: JsonValueKind.Array } array
                ? array.EnumerateArray().Select(ToTenant).Where(t => t is not null).Select(t => t!).ToList()
                : new List<ApiTenantInfo>();
            return ApiSunatCallResult<ApiTenantPage>.Ok(new ApiTenantPage(rows, call.Data.TotalRecords));
        }

        public async Task<ApiSunatCallResult<IReadOnlyList<ApiCertificateInfo>>> GetCertificatesAsync(string status, int days, CancellationToken cancellationToken)
        {
            var all = new List<ApiCertificateInfo>();
            // Páginas de 50, con tope de 40 (2 000 empresas): el aviso de certificados no necesita más.
            for (var page = 1; page <= 40; page++)
            {
                var path = $"api/admin/certificates?records=50&numPage={page}&dias={Math.Clamp(days, 0, 3650)}&status={Uri.EscapeDataString(status)}";
                var call = await SendAsync(HttpMethod.Get, path, null, null, cancellationToken);
                if (!call.Success)
                {
                    return ApiSunatCallResult<IReadOnlyList<ApiCertificateInfo>>.Fail(call.StatusCode, call.Error!, call.Retryable);
                }
                var rows = call.Data!.Data is { ValueKind: JsonValueKind.Array } array
                    ? array.EnumerateArray().Select(ToCertificate).Where(c => c is not null).Select(c => c!).ToList()
                    : new List<ApiCertificateInfo>();
                all.AddRange(rows);
                if (rows.Count == 0 || all.Count >= call.Data.TotalRecords)
                {
                    break;
                }
            }
            return ApiSunatCallResult<IReadOnlyList<ApiCertificateInfo>>.Ok(all);
        }

        public async Task<ApiSunatCallResult<ApiDashboardInfo>> GetDashboardAsync(CancellationToken cancellationToken)
        {
            var call = await SendAsync(HttpMethod.Get, "api/admin/dashboard", null, null, cancellationToken);
            if (!call.Success)
            {
                return ApiSunatCallResult<ApiDashboardInfo>.Fail(call.StatusCode, call.Error!, call.Retryable);
            }
            if (call.Data!.Data is not { ValueKind: JsonValueKind.Object } e)
            {
                return ApiSunatCallResult<ApiDashboardInfo>.Fail(502, "La API SUNAT respondió sin los datos del resumen.", true);
            }
            long Num(string name) => e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number && v.TryGetInt64(out var n) ? n : 0;
            return ApiSunatCallResult<ApiDashboardInfo>.Ok(new ApiDashboardInfo(
                Num("tenantsActive"), Num("documentsThisMonth"), Num("rejectedThisMonth"), Num("pendingNow"), Num("failedJobs"),
                Num("certificatesExpiring"), Num("certificatesBroken")));
        }

        public async Task<ApiSunatCallResult<long>> CountRejectionsAsync(DateOnly day, CancellationToken cancellationToken)
        {
            var text = day.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
            var call = await SendAsync(HttpMethod.Get, $"api/admin/rejections?startDate={text}&endDate={text}&records=1&numPage=1", null, null, cancellationToken);
            return call.Success
                ? ApiSunatCallResult<long>.Ok(call.Data!.TotalRecords)
                : ApiSunatCallResult<long>.Fail(call.StatusCode, call.Error!, call.Retryable);
        }

        private static ApiCertificateInfo? ToCertificate(JsonElement e)
        {
            if (e.ValueKind != JsonValueKind.Object || !e.TryGetProperty("tenantId", out var id) || id.ValueKind != JsonValueKind.Number)
            {
                return null;
            }
            string? Text(string name) => e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
            DateOnly? expires = DateOnly.TryParseExact(Text("expiresOn"), "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None, out var d) ? d : null;
            long? left = e.TryGetProperty("daysLeft", out var l) && l.ValueKind == JsonValueKind.Number && l.TryGetInt64(out var n) ? n : null;
            return new ApiCertificateInfo(id.GetInt64(), Text("ruc") ?? string.Empty, Text("razonSocial") ?? string.Empty, Text("environment"),
                Text("status") ?? string.Empty, expires, left);
        }

        public async Task<ApiSunatCallResult<ApiTenantInfo>> SetBillingSuspensionAsync(
            long tenantId, bool suspended, string reason, string actor, CancellationToken cancellationToken)
        {
            // activo = !suspended; tipo = pago: la consola solo suspende la emisión por falta de pago (la API rechaza otra cosa).
            var body = new Dictionary<string, object?> { ["activo"] = !suspended, ["tipo"] = "pago" };
            if (suspended)
            {
                body["motivo"] = reason;
            }
            var call = await SendAsync(HttpMethod.Put, $"api/admin/tenants/{tenantId}/estado", body, actor, cancellationToken);
            return ReadTenant(call);
        }

        public async Task<ApiSunatCallResult<ApiTenantInfo>> SetPlanAsync(
            long tenantId, string plan, int? maxDocumentsMonth, string reason, string actor, CancellationToken cancellationToken)
        {
            // maxDocumentsMonth vacío = sigue al cupo del plan; el modo de emisión no se envía (queda como está en la API).
            var body = new Dictionary<string, object?> { ["plan"] = plan, ["motivo"] = reason };
            if (maxDocumentsMonth is int max)
            {
                body["maxDocumentsMonth"] = max;
            }
            var call = await SendAsync(HttpMethod.Put, $"api/admin/tenants/{tenantId}/plan", body, actor, cancellationToken);
            return ReadTenant(call);
        }

        public async Task<ApiSunatCallResult<ApiPortalInvitation>> CreatePortalInvitationAsync(
            string email, string name, IReadOnlyCollection<long> tenantIds, bool resetMfa, string actor, CancellationToken cancellationToken)
        {
            // El token de la respuesta es secreto: se lee aquí y nunca se registra (SendAsync no registra cuerpos).
            var body = new Dictionary<string, object?> { ["email"] = email, ["name"] = name, ["tenantIds"] = tenantIds, ["resetMfa"] = resetMfa };
            var call = await SendAsync(HttpMethod.Post, "api/admin/portal/invitations", body, actor, cancellationToken);
            if (!call.Success)
            {
                return ApiSunatCallResult<ApiPortalInvitation>.Fail(call.StatusCode, call.Error!, call.Retryable);
            }
            if (call.Data!.Data is not { ValueKind: JsonValueKind.Object } data
                || !data.TryGetProperty("userId", out var id) || id.ValueKind != JsonValueKind.Number || !id.TryGetInt64(out var userId)
                || !data.TryGetProperty("token", out var token) || token.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(token.GetString())
                || !data.TryGetProperty("expiresAt", out var expires) || expires.ValueKind != JsonValueKind.String
                || !DateTime.TryParse(expires.GetString(), System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.AdjustToUniversal | System.Globalization.DateTimeStyles.AssumeUniversal, out var expiresAt))
            {
                // La API pudo haber creado al usuario: el reintento invita de nuevo y obtiene otro token.
                return ApiSunatCallResult<ApiPortalInvitation>.Fail(502, "La API SUNAT respondió la invitación sin los datos esperados.", true);
            }
            return ApiSunatCallResult<ApiPortalInvitation>.Ok(new ApiPortalInvitation(userId, token.GetString()!, expiresAt));
        }

        public async Task<ApiSunatCallResult<bool>> SetPortalUserActiveAsync(long userId, bool active, string actor, CancellationToken cancellationToken)
        {
            var call = await SendAsync(HttpMethod.Put, $"api/admin/portal/users/{userId}/estado", new Dictionary<string, object?> { ["active"] = active }, actor, cancellationToken);
            return call.Success
                ? ApiSunatCallResult<bool>.Ok(true)
                : ApiSunatCallResult<bool>.Fail(call.StatusCode, call.Error!, call.Retryable);
        }

        public async Task<ApiSunatCallResult<ApiTenantCredentials>> RegenerateTenantCredentialsAsync(
            long tenantId, string reason, string actor, CancellationToken cancellationToken)
        {
            // La respuesta lleva la key y el secret en claro: se leen aquí y nunca se registran (SendAsync no registra cuerpos).
            var call = await SendAsync(HttpMethod.Post, $"api/admin/tenants/{tenantId}/credenciales/regenerar",
                new Dictionary<string, object?> { ["motivo"] = reason }, actor, cancellationToken);
            if (!call.Success)
            {
                return ApiSunatCallResult<ApiTenantCredentials>.Fail(call.StatusCode, call.Error!, call.Retryable);
            }
            if (call.Data!.Data is not { ValueKind: JsonValueKind.Object } data
                || !data.TryGetProperty("apiKey", out var key) || key.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(key.GetString())
                || !data.TryGetProperty("apiSecret", out var secret) || secret.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(secret.GetString()))
            {
                // La API pudo haberlas regenerado: el reintento las regenera de nuevo y obtiene otras.
                return ApiSunatCallResult<ApiTenantCredentials>.Fail(502, "La API SUNAT respondió las credenciales sin los datos esperados.", true);
            }
            return ApiSunatCallResult<ApiTenantCredentials>.Ok(new ApiTenantCredentials(key.GetString()!, secret.GetString()!));
        }

        private static ApiSunatCallResult<ApiTenantInfo> ReadTenant(ApiSunatCallResult<Envelope> call)
        {
            if (!call.Success)
            {
                return ApiSunatCallResult<ApiTenantInfo>.Fail(call.StatusCode, call.Error!, call.Retryable);
            }
            var tenant = call.Data!.Data is { ValueKind: JsonValueKind.Object } obj ? ToTenant(obj) : null;
            return tenant is null
                ? ApiSunatCallResult<ApiTenantInfo>.Fail(502, "La API SUNAT respondió sin los datos de la empresa.", true)
                : ApiSunatCallResult<ApiTenantInfo>.Ok(tenant);
        }

        private static ApiTenantInfo? ToTenant(JsonElement e)
        {
            if (e.ValueKind != JsonValueKind.Object || !e.TryGetProperty("id", out var id) || id.ValueKind != JsonValueKind.Number)
            {
                return null;
            }
            string? Text(string name) => e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
            int Int(string name) => e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number && v.TryGetInt32(out var n) ? n : 0;
            bool Bool(string name, bool otherwise) => e.TryGetProperty(name, out var v) && v.ValueKind is JsonValueKind.True or JsonValueKind.False
                ? v.GetBoolean()
                : otherwise;

            return new ApiTenantInfo(
                id.GetInt64(),
                Text("ruc") ?? string.Empty,
                Text("razonSocial") ?? string.Empty,
                Text("nombreComercial"),
                Text("environment"),
                Text("plan"),
                Bool("isActive", true),
                Bool("billingSuspended", false),
                Int("documentsThisMonth"),
                Int("maxDocumentsMonth"));
        }

        private sealed record Envelope(bool Succeeded, string? Message, JsonElement? Data, long TotalRecords);

        private async Task<ApiSunatCallResult<Envelope>> SendAsync(
            HttpMethod method, string path, object? body, string? actor, CancellationToken cancellationToken)
        {
            if (!IsConfigured)
            {
                return ApiSunatCallResult<Envelope>.Fail(503, NotConfiguredMessage, true);
            }

            try
            {
                using var request = new HttpRequestMessage(method, new Uri(new Uri(_settings.BaseUrl!.TrimEnd('/') + "/"), path));
                request.Headers.Add("X-Admin-Key", _settings.AdminKey);
                request.Headers.Add("X-Admin-Actor", Sanitize(actor ?? "sistema"));
                if (body is not null)
                {
                    request.Content = JsonContent.Create(body, options: JsonOptions);
                }

                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeout.CancelAfter(TimeSpan.FromSeconds(Math.Clamp(_settings.TimeoutSeconds, 3, 120)));
                using var response = await _http.SendAsync(request, timeout.Token);
                var status = (int)response.StatusCode;
                var text = await response.Content.ReadAsStringAsync(timeout.Token);
                var envelope = TryParse(text);

                // 401 / 403: clave o IP aún sin configurar en alguno de los dos lados; se arregla sin tocar la acción, así que se reintenta.
                if (status is 401 or 403 or 408 or 429 || status >= 500)
                {
                    return ApiSunatCallResult<Envelope>.Fail(status, Describe(status, envelope), true);
                }
                if (response.StatusCode is HttpStatusCode.Moved or HttpStatusCode.Redirect or HttpStatusCode.RedirectKeepVerb)
                {
                    return ApiSunatCallResult<Envelope>.Fail(status, "La API SUNAT respondió con una redirección: revise ApiSunat:BaseUrl.", false);
                }
                if (envelope is null)
                {
                    return ApiSunatCallResult<Envelope>.Fail(status, $"La API SUNAT respondió algo inesperado (HTTP {status}).", status >= 500);
                }
                if (!response.IsSuccessStatusCode || !envelope.Succeeded)
                {
                    var message = envelope.Message ?? $"La API SUNAT rechazó la petición (HTTP {status}).";
                    return ApiSunatCallResult<Envelope>.Fail(message.Contains("No existe", StringComparison.OrdinalIgnoreCase) ? 404 : status, message, false);
                }
                return ApiSunatCallResult<Envelope>.Ok(envelope, status);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                return ApiSunatCallResult<Envelope>.Fail(408, "La API SUNAT no respondió a tiempo.", true);
            }
            catch (HttpRequestException ex)
            {
                // Nunca se registra la clave ni la URL con credenciales: solo el tipo de fallo.
                _logger.LogWarning("API SUNAT inaccesible ({Reason}).", ex.HttpRequestError);
                return ApiSunatCallResult<Envelope>.Fail(503, "No se pudo conectar con la API SUNAT.", true);
            }
        }

        private static Envelope? TryParse(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return null;
            }
            try
            {
                using var doc = JsonDocument.Parse(text);
                var root = doc.RootElement;
                if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("succeeded", out var ok))
                {
                    return null;
                }
                string? message = null;
                if (root.TryGetProperty("messages", out var messages) && messages.ValueKind == JsonValueKind.Array)
                {
                    message = string.Join(" ", messages.EnumerateArray().Where(m => m.ValueKind == JsonValueKind.String).Select(m => m.GetString()));
                }
                // Errores de validación por campo: se anexan al mensaje.
                if (root.TryGetProperty("errors", out var errors) && errors.ValueKind == JsonValueKind.Array)
                {
                    var fields = errors.EnumerateArray()
                        .Select(x => x.TryGetProperty("errorMessage", out var m) && m.ValueKind == JsonValueKind.String ? m.GetString() : null)
                        .Where(m => !string.IsNullOrWhiteSpace(m));
                    message = string.Join(" ", new[] { message }.Concat(fields).Where(m => !string.IsNullOrWhiteSpace(m)));
                }
                JsonElement? data = root.TryGetProperty("data", out var d) ? d.Clone() : null;
                var total = root.TryGetProperty("totalRecords", out var t) && t.ValueKind == JsonValueKind.Number && t.TryGetInt64(out var n) ? n : 0;
                return new Envelope(ok.ValueKind == JsonValueKind.True, string.IsNullOrWhiteSpace(message) ? null : message, data, total);
            }
            catch (JsonException)
            {
                return null;
            }
        }

        private static string Describe(int status, Envelope? envelope)
            => envelope?.Message ?? status switch
            {
                401 or 403 => "La API SUNAT rechazó la clave de servicio o la IP de la consola (revise ADMIN_SERVICE_KEY y ADMIN_SERVICE_ALLOWED_IPS).",
                429 => "La API SUNAT pidió esperar (demasiadas peticiones).",
                _ => $"La API SUNAT respondió con un error (HTTP {status})."
            };

        /// <summary>El actor viaja en una cabecera: sin saltos de línea ni caracteres de control, y de largo acotado.</summary>
        private static string Sanitize(string actor)
        {
            var clean = new string(actor.Where(c => !char.IsControl(c)).ToArray()).Trim();
            if (clean.Length == 0)
            {
                clean = "sistema";
            }
            return clean.Length > 100 ? clean[..100] : clean;
        }
    }
}
