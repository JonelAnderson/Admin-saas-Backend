using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using DS.Platform.WebApi.Extensions;
using Microsoft.Extensions.Configuration;

namespace DS.Platform.Tests.Security
{
    /// <summary>
    /// El certificado que cifra las llaves de Data Protection se lee de un archivo (Docker con un secreto montado) o, en Coolify,
    /// de la variable <c>DataProtection__CertificateBase64</c> (Coolify solo da variables de entorno, no archivos binarios).
    /// </summary>
    public class DataProtectionCertificateTests
    {
        private const string Password = "prueba-123";

        private static byte[] Pfx()
        {
            using var key = RSA.Create(2048);
            var request = new CertificateRequest("CN=erp-dp-test", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            using var cert = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(1));
            return cert.Export(X509ContentType.Pfx, Password);
        }

        private static IConfiguration Section(Dictionary<string, string?> values) =>
            new ConfigurationBuilder().AddInMemoryCollection(values).Build();

        [Fact]
        public void The_certificate_can_come_in_Base64_with_line_breaks()
        {
            var pfx = Pfx();
            var base64 = Convert.ToBase64String(pfx, Base64FormattingOptions.InsertLineBreaks);
            using var cert = SecretsExtensions.LoadCertificate(Section(new() { ["CertificateBase64"] = base64, ["CertificatePassword"] = Password }));
            Assert.NotNull(cert);
            Assert.True(cert!.HasPrivateKey);
            Assert.Equal("CN=erp-dp-test", cert.Subject);
        }

        [Fact]
        public void The_file_wins_over_Base64_and_nothing_set_means_no_certificate()
        {
            var path = Path.Combine(Path.GetTempPath(), $"erp-dp-{Guid.NewGuid():N}.pfx");
            File.WriteAllBytes(path, Pfx());
            try
            {
                using var cert = SecretsExtensions.LoadCertificate(Section(new()
                {
                    ["CertificatePath"] = path, ["CertificateBase64"] = "no-es-base64", ["CertificatePassword"] = Password
                }));
                Assert.NotNull(cert);
            }
            finally
            {
                File.Delete(path);
            }

            Assert.Null(SecretsExtensions.LoadCertificate(Section(new())));
        }

        [Fact]
        public void Invalid_Base64_says_how_to_build_it()
        {
            var error = Assert.Throws<InvalidOperationException>(() =>
                SecretsExtensions.LoadCertificate(Section(new() { ["CertificateBase64"] = "%%%", ["CertificatePassword"] = Password })));
            Assert.Contains("base64 -w0", error.Message);
        }
    }
}
