namespace DS.Platform.Application.Interfaces.Services.Coolify
{
    /// <summary>
    /// Configuración del aprovisionamiento (sección <c>Provisioning</c>; nada secreto). Sin <see cref="BaseDomain"/> o
    /// <see cref="ImageRepository"/> el aprovisionamiento está apagado y montar una instancia se rechaza con un mensaje claro.
    /// </summary>
    public interface IProvisioningSettings
    {
        /// <summary>Dominio base de las instancias: la de un cliente queda en <c>{subdominio}.{BaseDomain}</c>.</summary>
        string? BaseDomain { get; }

        /// <summary>Repositorio de imágenes (p. ej. <c>ghcr.io/cuenta</c>): <c>{repo}/erp-api</c>, <c>erp-migrator</c> y <c>erp-web</c>.</summary>
        string? ImageRepository { get; }

        /// <summary>Dirección pública de la consola, que cada ERP consulta para su licencia (<c>License__ControlUrl</c>).</summary>
        string? LicenseControlUrl { get; }

        /// <summary>Versión que se propone al montar una instancia (la última publicada).</summary>
        string? DefaultVersion { get; }

        bool IsConfigured { get; }
    }

    /// <summary>Secretos propios de un stack de cliente. Se generan una vez, se guardan cifrados hasta que el stack arranca y después solo viven en Coolify.</summary>
    public sealed record InstanceSecrets(
        string PostgresPassword,
        string JwtSecret,
        string InitialAdminPassword,
        string DataProtectionCertificateBase64,
        string DataProtectionCertificatePassword,
        string LicenseKey,
        string LicensePublicKey);

    /// <summary>Genera los secretos de un stack nuevo (todo al azar; el certificado de Data Protection es propio de cada cliente).</summary>
    public interface IProvisioningSecretsGenerator
    {
        /// <summary>Todo menos la clave de licencia y su llave pública, que pone quien llama.</summary>
        InstanceSecrets Generate(string licenseKey, string licensePublicKey);
    }
}
