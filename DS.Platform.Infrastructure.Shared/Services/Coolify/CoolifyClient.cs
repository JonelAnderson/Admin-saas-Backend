using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using DS.Platform.Application.Interfaces.Services.Coolify;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DS.Platform.Infrastructure.Shared.Services.Coolify
{
    /// <summary>
    /// Sección <c>Coolify</c>: dirección, proyecto y servidor van en appsettings / entorno; <c>Token</c> solo en user-secrets / variable de
    /// entorno (token de API de Coolify con permisos de escritura y despliegue).
    /// </summary>
    public class CoolifySettings
    {
        public const string SectionName = "Coolify";

        /// <summary>Dirección de Coolify (p. ej. https://coolify.midominio.pe). Vacía = integración apagada.</summary>
        public string? BaseUrl { get; set; }

        /// <summary>Token de API de Coolify. Nunca se registra en el log.</summary>
        public string? Token { get; set; }

        /// <summary>Proyecto de Coolify donde viven las instancias de los clientes (uuid).</summary>
        public string? ProjectUuid { get; set; }

        /// <summary>Servidor donde se montan (uuid).</summary>
        public string? ServerUuid { get; set; }

        /// <summary>Entorno del proyecto (por nombre).</summary>
        public string EnvironmentName { get; set; } = "production";

        /// <summary>Solo si el servidor tiene varios destinos de Docker (uuid).</summary>
        public string? DestinationUuid { get; set; }

        public int TimeoutSeconds { get; set; } = 30;
    }

    /// <summary>
    /// Cliente de la API de Coolify (<c>/api/v1</c>) con el token de portador. Nunca lanza: todo problema vuelve como
    /// <see cref="CoolifyCallResult{T}"/> con <c>Retryable</c> (red, tiempo agotado, 408, 429, 5xx y 401 / 403 por token aún sin configurar)
    /// o sin él (petición rechazada: proyecto inexistente, dominio en conflicto, validación). No sigue redirecciones: el token no puede
    /// viajar a otra dirección. Nunca registra el token, el cuerpo enviado ni los valores de las variables (pueden ser secretos).
    /// </summary>
    public class CoolifyClient : ICoolifyClient
    {
        public const string NotConfiguredMessage =
            "La integración con Coolify no está configurada (Coolify:BaseUrl, Coolify:Token, Coolify:ProjectUuid y Coolify:ServerUuid).";

        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
        {
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        };

        private readonly HttpClient _http;
        private readonly CoolifySettings _settings;
        private readonly ILogger<CoolifyClient> _logger;

        public CoolifyClient(HttpClient http, IOptions<CoolifySettings> settings, ILogger<CoolifyClient> logger)
        {
            _http = http;
            _settings = settings.Value;
            _logger = logger;
        }

        public bool IsConfigured => Uri.TryCreate(_settings.BaseUrl, UriKind.Absolute, out _)
                                    && !string.IsNullOrWhiteSpace(_settings.Token)
                                    && !string.IsNullOrWhiteSpace(_settings.ProjectUuid)
                                    && !string.IsNullOrWhiteSpace(_settings.ServerUuid);

        public async Task<CoolifyCallResult<string>> CreateServiceAsync(CoolifyServiceSpec spec, CancellationToken cancellationToken)
        {
            var body = new
            {
                name = spec.Name,
                description = spec.Description,
                project_uuid = _settings.ProjectUuid,
                server_uuid = _settings.ServerUuid,
                environment_name = _settings.EnvironmentName,
                destination_uuid = string.IsNullOrWhiteSpace(_settings.DestinationUuid) ? null : _settings.DestinationUuid,
                // El stack se arranca después de cargar sus variables (StartServiceAsync): nunca antes.
                instant_deploy = false,
                docker_compose_raw = Convert.ToBase64String(Encoding.UTF8.GetBytes(spec.ComposeYaml)),
                // El puerto es el del contenedor que recibe el tráfico (Traefik de Coolify enruta por dominio).
                urls = new[] { new { name = spec.ServiceName, url = $"{spec.PublicUrl.TrimEnd('/')}:{spec.ServicePort}" } }
            };
            var call = await SendAsync(HttpMethod.Post, "services", body, cancellationToken);
            if (!call.Success)
            {
                return CoolifyCallResult<string>.Fail(call.StatusCode, call.Error!, call.Retryable);
            }
            var uuid = ReadString(call.Data, "uuid");
            return string.IsNullOrWhiteSpace(uuid)
                ? CoolifyCallResult<string>.Fail(call.StatusCode, "Coolify creó el servicio pero no devolvió su uuid.", false)
                : CoolifyCallResult<string>.Ok(uuid, call.StatusCode);
        }

        public async Task<CoolifyCallResult<bool>> SetEnvAsync(string serviceUuid, string key, string value, CancellationToken cancellationToken)
        {
            var path = $"services/{Uri.EscapeDataString(serviceUuid)}/envs";
            // is_literal: el valor se guarda tal cual (un secreto con $ no se interpola).
            var body = new { key, value, is_literal = true };

            var update = await SendAsync(HttpMethod.Patch, path, body, cancellationToken);
            if (update.Success)
            {
                return CoolifyCallResult<bool>.Ok(true, update.StatusCode);
            }
            if (update.StatusCode != (int)HttpStatusCode.NotFound)
            {
                return CoolifyCallResult<bool>.Fail(update.StatusCode, update.Error!, update.Retryable);
            }

            // La variable aún no existe: se crea. Si el servicio no existe, esta llamada también da 404 y se rechaza.
            var create = await SendAsync(HttpMethod.Post, path, body, cancellationToken);
            return create.Success
                ? CoolifyCallResult<bool>.Ok(true, create.StatusCode)
                : CoolifyCallResult<bool>.Fail(create.StatusCode, create.Error!, create.Retryable);
        }

        public async Task<CoolifyCallResult<bool>> StartServiceAsync(string serviceUuid, CancellationToken cancellationToken)
        {
            var call = await SendAsync(HttpMethod.Post, $"services/{Uri.EscapeDataString(serviceUuid)}/start", null, cancellationToken);
            return call.Success ? CoolifyCallResult<bool>.Ok(true, call.StatusCode) : CoolifyCallResult<bool>.Fail(call.StatusCode, call.Error!, call.Retryable);
        }

        public async Task<CoolifyCallResult<bool>> RestartServiceAsync(string serviceUuid, bool pullLatest, CancellationToken cancellationToken)
        {
            var path = $"services/{Uri.EscapeDataString(serviceUuid)}/restart" + (pullLatest ? "?latest=true" : string.Empty);
            var call = await SendAsync(HttpMethod.Post, path, null, cancellationToken);
            return call.Success ? CoolifyCallResult<bool>.Ok(true, call.StatusCode) : CoolifyCallResult<bool>.Fail(call.StatusCode, call.Error!, call.Retryable);
        }

        private async Task<CoolifyCallResult<JsonElement?>> SendAsync(HttpMethod method, string path, object? body, CancellationToken cancellationToken)
        {
            if (!IsConfigured)
            {
                return CoolifyCallResult<JsonElement?>.Fail(503, NotConfiguredMessage, true);
            }

            try
            {
                using var request = new HttpRequestMessage(method, new Uri(new Uri(_settings.BaseUrl!.TrimEnd('/') + "/api/v1/"), path));
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _settings.Token);
                request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                if (body is not null)
                {
                    request.Content = JsonContent.Create(body, options: JsonOptions);
                }

                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeout.CancelAfter(TimeSpan.FromSeconds(Math.Clamp(_settings.TimeoutSeconds, 5, 180)));
                using var response = await _http.SendAsync(request, timeout.Token);
                var status = (int)response.StatusCode;
                var text = await response.Content.ReadAsStringAsync(timeout.Token);
                var json = TryParse(text);

                // 401 / 403: token aún sin configurar o sin permisos; se arregla sin tocar el despliegue, así que se reintenta.
                if (status is 401 or 403 or 408 or 429 || status >= 500)
                {
                    return CoolifyCallResult<JsonElement?>.Fail(status, Describe(status, json), true);
                }
                if (response.StatusCode is HttpStatusCode.Moved or HttpStatusCode.Redirect or HttpStatusCode.RedirectKeepVerb or HttpStatusCode.PermanentRedirect)
                {
                    return CoolifyCallResult<JsonElement?>.Fail(status, "Coolify respondió con una redirección: revise Coolify:BaseUrl.", false);
                }
                if (!response.IsSuccessStatusCode)
                {
                    return CoolifyCallResult<JsonElement?>.Fail(status, Describe(status, json), false);
                }
                return CoolifyCallResult<JsonElement?>.Ok(json, status);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                return CoolifyCallResult<JsonElement?>.Fail(408, "Coolify no respondió a tiempo.", true);
            }
            catch (HttpRequestException ex)
            {
                // Nunca se registra el token ni la URL: solo el tipo de fallo.
                _logger.LogWarning("Coolify inaccesible ({Reason}).", ex.HttpRequestError);
                return CoolifyCallResult<JsonElement?>.Fail(503, "No se pudo conectar con Coolify.", true);
            }
        }

        private static JsonElement? TryParse(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return null;
            }
            try
            {
                using var doc = JsonDocument.Parse(text);
                return doc.RootElement.Clone();
            }
            catch (JsonException)
            {
                return null;
            }
        }

        private static string? ReadString(JsonElement? json, string property)
            => json is { ValueKind: JsonValueKind.Object } obj && obj.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
                ? value.GetString()
                : null;

        /// <summary>Mensaje de Coolify (<c>message</c>) y, si hay, los campos que rechazó (solo sus nombres, nunca los valores).</summary>
        private static string Describe(int status, JsonElement? json)
        {
            var message = ReadString(json, "message");
            var fields = json is { ValueKind: JsonValueKind.Object } obj && obj.TryGetProperty("errors", out var errors) && errors.ValueKind == JsonValueKind.Object
                ? string.Join(", ", errors.EnumerateObject().Select(p => p.Name))
                : null;
            var text = string.IsNullOrWhiteSpace(message) ? $"Coolify respondió HTTP {status}." : $"Coolify: {message}";
            return string.IsNullOrEmpty(fields) ? text : $"{text} (campos: {fields})";
        }
    }
}
