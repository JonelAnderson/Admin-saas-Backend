namespace DS.Platform.Application.DTOs.Identities.Response
{
    public class MfaSetupResponse
    {
        /// <summary>Clave en grupos de 4 para ingresarla a mano en la app.</summary>
        public string SharedKey { get; set; } = string.Empty;

        /// <summary>otpauth://totp/DS.Platform:{usuario}?secret=…&amp;issuer=DS.Platform&amp;digits=6&amp;period=30 (el frontend dibuja el QR).</summary>
        public string AuthenticatorUri { get; set; } = string.Empty;
    }
}
