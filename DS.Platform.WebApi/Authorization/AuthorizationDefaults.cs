using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;

namespace DS.Platform.WebApi.Authorization
{
    /// <summary>
    /// Política por defecto: todo endpoint sin <c>[Authorize]</c> ni <c>[AllowAnonymous]</c> exige un usuario autenticado con
    /// el JWT. Así un controlador nuevo que olvide su atributo no queda abierto a internet (antes pasaba con SunatReniec, que
    /// consulta una API de pago). Los anónimos permitidos están en <see cref="AnonymousEndpoints"/>.
    /// </summary>
    public static class AuthorizationDefaults
    {
        public static void Configure(AuthorizationOptions options)
        {
            options.FallbackPolicy = new AuthorizationPolicyBuilder(JwtBearerDefaults.AuthenticationScheme)
                .RequireAuthenticatedUser()
                .Build();
        }

        /// <summary>Únicas acciones con <c>[AllowAnonymous]</c> (las vigila <c>EndpointAuthorizationTests</c>).</summary>
        public static readonly string[] AnonymousEndpoints =
        [
            "TokenController.Get",
            "TokenController.LoginMfa",
            "TokenController.BeginMfaSetup",
            "TokenController.ConfirmMfaSetup",
            "TokenController.Refresh",
            "UserController.ConfirmEmailAsync",
            "UserController.ForgotPasswordAsync",
            "UserController.ResetPasswordAsync",
            // Licencias de las instancias del ERP: se identifican con su clave (X-License-Key) y hay límite por IP.
            "LicenseController.Status",
            "LicenseController.PublicKey",
            // Entrega de credenciales de la API SUNAT al ERP (fase C6b): misma clave de licencia y mismo límite por IP.
            "LicenseController.ApiSunatPull",
            "LicenseController.ApiSunatAck",
        ];
    }
}
