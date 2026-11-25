using System.Text.Json;
using System.Text.RegularExpressions;
using DS.Platform.Application.Interfaces.Services.Coolify;

namespace DS.Platform.Application.Features.Deployments.Common
{
    /// <summary>Reglas puras del aprovisionamiento: subdominio, versión y nombre del stack. No tocan datos ni red.</summary>
    public static partial class ProvisioningRules
    {
        public const string NotConfiguredMessage =
            "El aprovisionamiento no está configurado: faltan Provisioning:BaseDomain, Provisioning:ImageRepository y Provisioning:LicenseControlUrl.";
        public const string InvalidSubdomainMessage =
            "El subdominio solo lleva minúsculas, números y guiones (2 a 30 caracteres, sin guion al inicio ni al final).";
        public const string ReservedSubdomainMessage = "Ese subdominio está reservado.";
        public const string InvalidVersionMessage =
            "La versión es la etiqueta de las imágenes (p. ej. 1.4.0): letras, números, punto, guion y guion bajo, hasta 40 caracteres. No se acepta \"latest\": la versión debe ser rastreable.";
        public const string HostTakenMessage = "Otra instancia ya usa esa dirección.";

        /// <summary>Subdominios que nunca se asignan a un cliente: son de la plataforma.</summary>
        private static readonly HashSet<string> Reserved = new(StringComparer.Ordinal)
        {
            "www", "api", "admin", "control", "consola", "console", "erp", "app", "mail", "smtp", "sunat", "api-sunat",
            "coolify", "traefik", "db", "postgres", "ftp", "ns1", "ns2", "status", "portal", "login", "auth"
        };

        [GeneratedRegex("^[a-z0-9]([a-z0-9-]{0,28}[a-z0-9])$", RegexOptions.CultureInvariant)]
        private static partial Regex SubdomainRegex();

        [GeneratedRegex("^[A-Za-z0-9][A-Za-z0-9._-]{0,39}$", RegexOptions.CultureInvariant)]
        private static partial Regex VersionRegex();

        /// <summary>Normaliza lo escrito (sin espacios, en minúsculas) y valida; devuelve el mensaje de error o null si es válido.</summary>
        public static string? ValidateSubdomain(string? subdomain, out string normalized)
        {
            normalized = (subdomain ?? string.Empty).Trim().ToLowerInvariant();
            if (!SubdomainRegex().IsMatch(normalized))
            {
                return InvalidSubdomainMessage;
            }
            return Reserved.Contains(normalized) ? ReservedSubdomainMessage : null;
        }

        public static string? ValidateVersion(string? version, out string normalized)
        {
            normalized = (version ?? string.Empty).Trim();
            if (!VersionRegex().IsMatch(normalized) || normalized.Equals("latest", StringComparison.OrdinalIgnoreCase))
            {
                return InvalidVersionMessage;
            }
            return null;
        }

        public static string HostFor(string subdomain, string baseDomain) => $"{subdomain}.{baseDomain.Trim().Trim('.').ToLowerInvariant()}";

        public static string UrlFor(string host) => $"https://{host}";

        /// <summary>Nombre del servicio en Coolify: reconocible y único por instancia.</summary>
        public static string ServiceName(string host, int instanceId) => $"erp-{host.Split('.')[0]}-{instanceId}";

        /// <summary>Host de una URL guardada (para saber si dos instancias usan el mismo); null si no es una URL absoluta.</summary>
        public static string? HostOf(string? url)
            => Uri.TryCreate(url?.Trim(), UriKind.Absolute, out var uri) ? uri.Host.ToLowerInvariant() : null;
    }

    /// <summary>Los secretos del stack viajan en la fila del despliegue como JSON cifrado con <c>ISecretProtector</c>.</summary>
    public static class ProvisioningSecretsJson
    {
        private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

        public static string Serialize(InstanceSecrets secrets) => JsonSerializer.Serialize(secrets, Options);

        public static InstanceSecrets? Deserialize(string json)
        {
            try
            {
                return JsonSerializer.Deserialize<InstanceSecrets>(json, Options);
            }
            catch (JsonException)
            {
                return null;
            }
        }
    }
}
