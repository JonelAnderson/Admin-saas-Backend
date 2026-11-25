using System;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using DS.Platform.Application.Interfaces.Services.Coolify;
using Microsoft.Extensions.Options;

namespace DS.Platform.Infrastructure.Shared.Services.Coolify
{
    /// <summary>Sección <c>Provisioning</c> (nada secreto): dónde viven las instancias y qué imágenes se despliegan.</summary>
    public class ProvisioningOptions
    {
        public const string SectionName = "Provisioning";

        public string? BaseDomain { get; set; }
        public string? ImageRepository { get; set; }
        public string? LicenseControlUrl { get; set; }
        public string? DefaultVersion { get; set; }
    }

    public class ProvisioningSettings(IOptions<ProvisioningOptions> options) : IProvisioningSettings
    {
        private readonly ProvisioningOptions _options = options.Value;

        public string? BaseDomain => _options.BaseDomain;
        public string? ImageRepository => _options.ImageRepository;
        public string? LicenseControlUrl => _options.LicenseControlUrl;
        public string? DefaultVersion => _options.DefaultVersion;

        public bool IsConfigured => !string.IsNullOrWhiteSpace(BaseDomain)
                                    && !string.IsNullOrWhiteSpace(ImageRepository)
                                    && Uri.TryCreate(LicenseControlUrl, UriKind.Absolute, out var control)
                                    && control.Scheme == Uri.UriSchemeHttps;
    }

    /// <summary>
    /// Genera los secretos de un stack de cliente con el generador criptográfico del sistema. El certificado de Data Protection es
    /// propio de cada cliente (RSA 3072, autofirmado, 10 años, como <c>generate-dp-cert.sh</c> del ERP). La contraseña inicial cumple
    /// la política del ERP (8 a 16 caracteres con mayúscula, minúscula, número y símbolo) y evita los caracteres que rompen un
    /// compose o un YAML (<c>$ " ' \ #</c>).
    /// </summary>
    public class ProvisioningSecretsGenerator : IProvisioningSecretsGenerator
    {
        private const string Lower = "abcdefghijkmnopqrstuvwxyz";
        private const string Upper = "ABCDEFGHJKLMNPQRSTUVWXYZ";
        private const string Digits = "23456789";
        private const string Symbols = "!@%*-_+=?";
        private const string Alphanumeric = Lower + Upper + Digits;

        public InstanceSecrets Generate(string licenseKey, string licensePublicKey)
        {
            var certificatePassword = RandomText(32, Alphanumeric);
            return new InstanceSecrets(
                PostgresPassword: RandomText(32, Alphanumeric),
                JwtSecret: Convert.ToBase64String(RandomNumberGenerator.GetBytes(48)),
                InitialAdminPassword: InitialPassword(),
                DataProtectionCertificateBase64: NewCertificate(certificatePassword),
                DataProtectionCertificatePassword: certificatePassword,
                LicenseKey: licenseKey,
                LicensePublicKey: licensePublicKey);
        }

        /// <summary>16 caracteres: al menos uno de cada clase, mezclados, y el primero siempre alfanumérico.</summary>
        public static string InitialPassword()
        {
            var chars = new char[16];
            chars[0] = Pick(Alphanumeric);
            chars[1] = Pick(Lower);
            chars[2] = Pick(Upper);
            chars[3] = Pick(Digits);
            chars[4] = Pick(Symbols);
            for (var i = 5; i < chars.Length; i++)
            {
                chars[i] = Pick(Alphanumeric + Symbols);
            }
            // Mezcla (Fisher-Yates) salvo la primera posición, que se queda alfanumérica.
            for (var i = chars.Length - 1; i > 1; i--)
            {
                var j = RandomNumberGenerator.GetInt32(1, i + 1);
                (chars[i], chars[j]) = (chars[j], chars[i]);
            }
            return new string(chars);
        }

        private static string NewCertificate(string password)
        {
            using var rsa = RSA.Create(3072);
            var request = new CertificateRequest("CN=erp-dataprotection", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            using var certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(10));
            return Convert.ToBase64String(certificate.Export(X509ContentType.Pfx, password));
        }

        private static string RandomText(int length, string alphabet)
        {
            var chars = new char[length];
            for (var i = 0; i < length; i++)
            {
                chars[i] = Pick(alphabet);
            }
            return new string(chars);
        }

        private static char Pick(string alphabet) => alphabet[RandomNumberGenerator.GetInt32(alphabet.Length)];
    }
}
