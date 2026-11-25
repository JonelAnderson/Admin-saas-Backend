using System.Text;
using Microsoft.AspNetCore.WebUtilities;

namespace DS.Platform.Infrastructure.Services.Identity
{
    /// <summary>
    /// El token de "olvidé mi contraseña" viaja en el enlace del correo en Base64Url (el de Identity lleva "+", "/" y "=").
    /// Al restablecer se decodifica antes de pasarlo a Identity: si no, el token nunca coincide.
    /// </summary>
    public static class PasswordResetToken
    {
        public static string Encode(string identityToken) => WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(identityToken));

        /// <summary>Token de Identity, o null si el texto del enlace no es Base64Url válido.</summary>
        public static string? TryDecode(string? linkToken)
        {
            if (string.IsNullOrWhiteSpace(linkToken))
                return null;
            try
            {
                return Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(linkToken.Trim()));
            }
            catch (FormatException)
            {
                return null;
            }
        }
    }
}
