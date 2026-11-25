using Hangfire.Dashboard;
using Microsoft.AspNetCore.Http;
using System.Net;

namespace DS.Platform.WebApi.Filters
{
    /// <summary>
    /// Panel de Hangfire (<c>/jobs</c>): solo desde el mismo servidor, sin pasar por un proxy. El panel no lleva el JWT del
    /// frontend (es una página del navegador) y muestra los argumentos de los jobs, como los correos de restablecer contraseña,
    /// así que nunca se abre a la red. Fuera de Development ni siquiera se monta salvo <c>Hangfire:DashboardEnabled = true</c>.
    /// </summary>
    public class HangfireAuthorizationFilter : IDashboardAuthorizationFilter
    {
        public bool Authorize(DashboardContext context) => IsLocalRequest(context.GetHttpContext());

        public static bool IsLocalRequest(HttpContext http)
        {
            // Detrás de un proxy la IP de origen sería la del proxy (local): un pedido reenviado nunca cuenta como local.
            if (http.Request.Headers.ContainsKey("X-Forwarded-For") || http.Request.Headers.ContainsKey("Forwarded"))
            {
                return false;
            }

            var remote = http.Connection.RemoteIpAddress;
            return remote is not null && IPAddress.IsLoopback(remote);
        }
    }
}
