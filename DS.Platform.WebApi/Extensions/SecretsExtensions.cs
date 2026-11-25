using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography.X509Certificates;

namespace DS.Platform.WebApi.Extensions
{
    /// <summary>
    /// Secrets policy:
    /// - configuration secrets never live in appsettings*.json: User Secrets (development) or environment variables (servers);
    /// - secrets stored in the database are encrypted with Data Protection (<c>ISecretProtector</c>).
    /// </summary>
    internal static class SecretsExtensions
    {
        private const string ApplicationName = "DS.Platform";

        /// <summary>Required to start; optional integrations (mail, SUNAT/RENIEC, Equifax) only fail when used.</summary>
        private static readonly string[] RequiredSecrets =
        [
            "ConnectionStrings:DefaultConnection",
            "AppConfiguration:Secret"
        ];

        private const int MinimumJwtSecretLength = 32;

        internal static void ValidateRequiredSecrets(IConfiguration configuration)
        {
            var missing = RequiredSecrets.Where(key => string.IsNullOrWhiteSpace(configuration[key])).ToList();
            var problems = new List<string>(missing.Select(key => $"'{key}' is not set"));

            var jwtSecret = configuration["AppConfiguration:Secret"];
            if (!string.IsNullOrWhiteSpace(jwtSecret) && jwtSecret.Length < MinimumJwtSecretLength)
            {
                problems.Add($"'AppConfiguration:Secret' must have at least {MinimumJwtSecretLength} characters");
            }

            if (problems.Count > 0)
            {
                throw new InvalidOperationException(
                    "Missing or invalid secrets: " + string.Join("; ", problems) + ". " +
                    "Development: dotnet user-secrets set \"<key>\" \"<value>\" --project DS.Platform.WebApi. " +
                    "Servers: environment variables with '__' as separator (e.g. ConnectionStrings__DefaultConnection).");
            }
        }

        /// <summary>
        /// Key ring for <c>ISecretProtector</c> (and Identity tokens). Keys rotate every 90 days; old keys keep
        /// decrypting. Back up the keys directory: without it, stored API tokens cannot be decrypted.
        /// </summary>
        internal static IServiceCollection AddErpDataProtection(
            this IServiceCollection services,
            IConfiguration configuration,
            IHostEnvironment environment)
        {
            var section = configuration.GetSection("DataProtection");

            var keysPath = section["KeysPath"];
            if (string.IsNullOrWhiteSpace(keysPath))
            {
                keysPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    ApplicationName,
                    "DataProtection-Keys");
            }

            var builder = services.AddDataProtection()
                .SetApplicationName(ApplicationName)
                .PersistKeysToFileSystem(new DirectoryInfo(keysPath));

            // Keys at rest: certificate (any OS, required on Linux servers) > Windows DPAPI (current user).
            var certificate = LoadCertificate(section);
            if (certificate is not null)
            {
                builder.ProtectKeysWithCertificate(certificate);
            }
            else if (OperatingSystem.IsWindows())
            {
                builder.ProtectKeysWithDpapi();
            }
            else if (!environment.IsDevelopment())
            {
                throw new InvalidOperationException(
                    "DataProtection:CertificatePath or DataProtection:CertificateBase64 (and DataProtection:CertificatePassword) " +
                    "is required outside Windows so the key ring is not stored unencrypted.");
            }

            return services;
        }

        /// <summary>
        /// The .pfx that protects the key ring: a file (<c>CertificatePath</c>, Docker with a mounted secret) or its bytes in
        /// Base64 (<c>CertificateBase64</c>, for hosts such as Coolify that only take environment variables). The file wins.
        /// </summary>
        internal static X509Certificate2? LoadCertificate(IConfiguration section)
        {
            var password = section["CertificatePassword"];
            var certificatePath = section["CertificatePath"];
            if (!string.IsNullOrWhiteSpace(certificatePath))
                return X509CertificateLoader.LoadPkcs12FromFile(certificatePath, password);

            var base64 = section["CertificateBase64"];
            if (string.IsNullOrWhiteSpace(base64))
                return null;

            byte[] bytes;
            try
            {
                bytes = Convert.FromBase64String(string.Concat(base64.Where(c => !char.IsWhiteSpace(c))));
            }
            catch (FormatException)
            {
                throw new InvalidOperationException("DataProtection:CertificateBase64 is not valid Base64 (use: base64 -w0 dp-cert.pfx).");
            }
            return X509CertificateLoader.LoadPkcs12(bytes, password);
        }
    }
}
