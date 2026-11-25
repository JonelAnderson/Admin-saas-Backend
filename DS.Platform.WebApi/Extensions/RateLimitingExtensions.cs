using System;
using System.Threading.RateLimiting;
using DS.Platform.WebApi.Controllers.v1;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.DependencyInjection;

namespace DS.Platform.WebApi.Extensions
{
    public static class RateLimitingExtensions
    {
        /// <summary>Consultas de licencia por IP y por minuto: una instancia consulta cada 10 min, así que 30 deja margen de sobra.</summary>
        public const int LicensePermitPerMinute = 30;

        /// <summary>
        /// Límite por IP del endpoint de licencias (el único anónimo y expuesto a internet): frena a quien pruebe claves. Detrás del
        /// proxy de confianza la IP real sale de <c>UseForwarding</c> (<c>AppConfiguration:ProxyIP</c>).
        /// </summary>
        public static IServiceCollection AddLicenseRateLimiting(this IServiceCollection services)
            => services.AddRateLimiter(options =>
            {
                options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
                options.AddPolicy(LicenseController.RateLimitPolicy, http => RateLimitPartition.GetFixedWindowLimiter(
                    http.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = LicensePermitPerMinute,
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0
                    }));
            });
    }
}
