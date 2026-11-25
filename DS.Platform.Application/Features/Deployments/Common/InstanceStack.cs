using DS.Platform.Application.Interfaces.Services.Coolify;

namespace DS.Platform.Application.Features.Deployments.Common
{
    /// <summary>
    /// El stack Docker Compose de la instancia de un cliente y sus variables (fase C5). Es puro: no tiene secretos propios ni sabe
    /// de HTTP. El compose es el mismo para todos los clientes y no cambia con la versión: las imágenes y todo lo demás salen de las
    /// variables del servicio en Coolify, así actualizar es cambiar <c>ERP_VERSION</c> y reiniciar.
    /// Basado en <c>ERP-Backend/docker-compose.coolify.yml</c>, con imágenes ya publicadas (<c>image:</c>, sin <c>build:</c>), sin
    /// puertos publicados (varios clientes comparten el servidor; Traefik de Coolify enruta por dominio), con la web dentro del
    /// stack y sin el worker de IA (opcional, recurso aparte).
    /// </summary>
    public static class InstanceStack
    {
        /// <summary>Servicio que Coolify publica con el dominio del cliente y el puerto en el que escucha.</summary>
        public const string PublicService = "web";
        public const int PublicPort = 80;

        public static readonly string[] ImageNames = ["erp-api", "erp-migrator", "erp-web"];

        public const string ComposeYaml = """
            # ERP MultiEmpresa: instancia de un cliente. Lo genera la consola de plataforma (fase C5); no se edita a mano en Coolify.
            # Todo sale de las variables del servicio. Sin "${VAR:?}": Coolify interpola el archivo también al compilar.
            # Los valores secretos van entre comillas: una contraseña que empiece con ! * o @ rompería el YAML al interpolarse.
            x-db-env: &db-env
              PGHOST: db
              PGPORT: "5432"
              PGDATABASE: ${POSTGRES_DB}
              PGUSER: ${POSTGRES_USER}
              PGPASSWORD: "${POSTGRES_PASSWORD}"

            services:
              db:
                image: postgres:18-alpine
                restart: unless-stopped
                environment:
                  POSTGRES_DB: ${POSTGRES_DB}
                  POSTGRES_USER: ${POSTGRES_USER}
                  POSTGRES_PASSWORD: "${POSTGRES_PASSWORD}"
                  TZ: America/Lima
                volumes:
                  - erp-db-data:/var/lib/postgresql
                  - erp-db-backups:/backups
                healthcheck:
                  test: ["CMD-SHELL", "pg_isready -U $${POSTGRES_USER} -d $${POSTGRES_DB}"]
                  interval: 10s
                  timeout: 5s
                  retries: 10

              migrate:
                image: ${ERP_IMAGE_REPO}/erp-migrator:${ERP_VERSION}
                environment: *db-env
                command: ["migrate"]
                depends_on:
                  db:
                    condition: service_healthy
                restart: "no"
                exclude_from_hc: true

              api:
                image: ${ERP_IMAGE_REPO}/erp-api:${ERP_VERSION}
                restart: unless-stopped
                depends_on:
                  migrate:
                    condition: service_completed_successfully
                environment:
                  ConnectionStrings__DefaultConnection: "Host=db;Port=5432;Database=${POSTGRES_DB};Username=${POSTGRES_USER};Password=${POSTGRES_PASSWORD}"
                  AppConfiguration__Secret: "${JWT_SECRET}"
                  AppConfiguration__ApplicationUrl: "${ERP_PUBLIC_URL}"
                  AppConfiguration__BehindSSLProxy: "true"
                  AppConfiguration__ProxyIP: "10.0.0.0/8,172.16.0.0/12,192.168.0.0/16"
                  Seed__InitialAdminPassword: "${ERP_INITIAL_ADMIN_PASSWORD}"
                  DataProtection__KeysPath: /app/dp-keys
                  DataProtection__CertificateBase64: "${DP_CERT_BASE64}"
                  DataProtection__CertificatePassword: "${DP_CERT_PASSWORD}"
                  License__ControlUrl: "${LICENSE_CONTROL_URL}"
                  License__Key: "${LICENSE_KEY}"
                  License__PublicKey: "${LICENSE_PUBLIC_KEY}"
                volumes:
                  - erp-uploads:/app/wwwroot
                  - erp-dp-keys:/app/dp-keys
                  - erp-api-logs:/app/Logs

              menus:
                image: ${ERP_IMAGE_REPO}/erp-migrator:${ERP_VERSION}
                environment: *db-env
                command: ["menus"]
                depends_on:
                  api:
                    condition: service_healthy
                restart: "no"
                exclude_from_hc: true

              web:
                image: ${ERP_IMAGE_REPO}/erp-web:${ERP_VERSION}
                restart: unless-stopped
                depends_on:
                  api:
                    condition: service_healthy

            volumes:
              erp-db-data:
              erp-db-backups:
              erp-uploads:
              erp-dp-keys:
              erp-api-logs:
            """;

        /// <summary>
        /// Variables de un stack nuevo. Todas obligatorias: ninguna queda vacía. <paramref name="host"/> es la dirección pública y
        /// <paramref name="version"/> la etiqueta de las imágenes.
        /// </summary>
        public static IReadOnlyDictionary<string, string> Environment(
            InstanceSecrets secrets, IProvisioningSettings settings, string host, string version)
            => new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["POSTGRES_DB"] = "erp",
                ["POSTGRES_USER"] = "erp",
                ["POSTGRES_PASSWORD"] = secrets.PostgresPassword,
                ["JWT_SECRET"] = secrets.JwtSecret,
                ["ERP_PUBLIC_URL"] = ProvisioningRules.UrlFor(host),
                ["ERP_INITIAL_ADMIN_PASSWORD"] = secrets.InitialAdminPassword,
                ["DP_CERT_BASE64"] = secrets.DataProtectionCertificateBase64,
                ["DP_CERT_PASSWORD"] = secrets.DataProtectionCertificatePassword,
                ["LICENSE_CONTROL_URL"] = settings.LicenseControlUrl!.Trim().TrimEnd('/'),
                ["LICENSE_KEY"] = secrets.LicenseKey,
                ["LICENSE_PUBLIC_KEY"] = secrets.LicensePublicKey,
                ["ERP_IMAGE_REPO"] = settings.ImageRepository!.Trim().TrimEnd('/'),
                ["ERP_VERSION"] = version
            };
    }
}
