namespace DS.Platform.Application.Interfaces.Services.Licensing
{
    /// <summary>
    /// Firma de las licencias que recibe cada ERP (Ed25519). La llave privada vive solo en la configuración de la consola
    /// (<c>License__SigningKey</c>, 32 bytes en base64), nunca en la base; cada ERP tiene la llave pública.
    /// </summary>
    public interface ILicenseSigner
    {
        /// <summary>false si no hay llave configurada: la consola no entrega licencias (nunca firma con una llave de reserva).</summary>
        bool IsConfigured { get; }

        /// <summary>Llave pública (32 bytes en base64) que se pone en cada ERP; vacía si no hay llave.</summary>
        string PublicKey { get; }

        /// <summary>Firma los bytes y devuelve la firma en base64.</summary>
        string Sign(byte[] data);
    }
}
