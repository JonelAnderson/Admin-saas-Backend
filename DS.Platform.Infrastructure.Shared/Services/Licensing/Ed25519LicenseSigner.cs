using System;
using DS.Platform.Application.Interfaces.Services.Licensing;
using Microsoft.Extensions.Options;
using Org.BouncyCastle.Math.EC.Rfc8032;

namespace DS.Platform.Infrastructure.Shared.Services.Licensing
{
    /// <summary>Sección <c>License</c>: <c>SigningKey</c> (32 bytes en base64) solo en user-secrets / variable de entorno.</summary>
    public class LicenseSettings
    {
        public const string SectionName = "License";

        /// <summary>Semilla privada Ed25519 (32 bytes en base64). Nunca en appsettings ni en la base.</summary>
        public string? SigningKey { get; set; }
    }

    /// <summary>
    /// Firma Ed25519 (RFC 8032) de las licencias. Sin llave válida queda <see cref="IsConfigured"/> en false y la consola no entrega
    /// licencias (nunca firma con una llave de reserva). Generar una llave: <c>openssl rand -base64 32</c>; la pública sale de
    /// <c>GET api/v1/License/PublicKey</c> y se pone en cada ERP.
    /// </summary>
    public sealed class Ed25519LicenseSigner : ILicenseSigner
    {
        private readonly byte[]? _seed;
        private readonly string _publicKey = string.Empty;

        public Ed25519LicenseSigner(IOptions<LicenseSettings> settings)
        {
            _seed = Parse(settings.Value.SigningKey);
            if (_seed is not null)
            {
                var publicKey = new byte[Ed25519.PublicKeySize];
                Ed25519.GeneratePublicKey(_seed, 0, publicKey, 0);
                _publicKey = Convert.ToBase64String(publicKey);
            }
        }

        public bool IsConfigured => _seed is not null;

        public string PublicKey => _publicKey;

        public string Sign(byte[] data)
        {
            if (_seed is null)
            {
                throw new InvalidOperationException("La llave de firma de licencias no está configurada (License:SigningKey).");
            }
            var signature = new byte[Ed25519.SignatureSize];
            Ed25519.Sign(_seed, 0, data, 0, data.Length, signature, 0);
            return Convert.ToBase64String(signature);
        }

        private static byte[]? Parse(string? base64)
        {
            if (string.IsNullOrWhiteSpace(base64))
            {
                return null;
            }
            try
            {
                var bytes = Convert.FromBase64String(base64.Trim());
                return bytes.Length == Ed25519.SecretKeySize ? bytes : null;
            }
            catch (FormatException)
            {
                return null;
            }
        }
    }
}
