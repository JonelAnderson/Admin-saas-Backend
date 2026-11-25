using DS.Platform.WebApi.Authorization;
using DS.Platform.WebApi.Filters;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Net;
using System.Reflection;

namespace DS.Platform.Tests.Security
{
    /// <summary>
    /// Ningún endpoint queda abierto por olvido: la política por defecto exige usuario autenticado, los únicos
    /// [AllowAnonymous] son los del login y la recuperación de contraseña, y el panel de Hangfire solo responde en el servidor.
    /// </summary>
    public class EndpointAuthorizationTests
    {
        private static IEnumerable<MethodInfo> Actions() => typeof(DS.Platform.WebApi.Controllers.MetaController).Assembly
            .GetTypes()
            .Where(t => typeof(ControllerBase).IsAssignableFrom(t) && !t.IsAbstract)
            .SelectMany(t => t.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly))
            .Where(m => m.GetCustomAttributes<Microsoft.AspNetCore.Mvc.Routing.HttpMethodAttribute>().Any());

        private static string Name(MethodInfo m) => $"{m.DeclaringType!.Name}.{m.Name}";

        [Fact]
        public void Only_login_and_password_recovery_are_anonymous()
        {
            var anonymous = Actions()
                .Where(m => m.GetCustomAttribute<AllowAnonymousAttribute>() is not null
                            || m.DeclaringType!.GetCustomAttribute<AllowAnonymousAttribute>() is not null)
                .Select(Name)
                .OrderBy(n => n)
                .ToList();
            Assert.Equal(AuthorizationDefaults.AnonymousEndpoints.OrderBy(n => n), anonymous);
        }

        [Fact]
        public void The_default_policy_requires_an_authenticated_user()
        {
            var options = new AuthorizationOptions();
            AuthorizationDefaults.Configure(options);
            Assert.NotNull(options.FallbackPolicy);
            Assert.Contains(options.FallbackPolicy!.Requirements, r => r is Microsoft.AspNetCore.Authorization.Infrastructure.DenyAnonymousAuthorizationRequirement);
        }

        [Theory]
        [InlineData("127.0.0.1", null, true)]
        [InlineData("::1", null, true)]
        [InlineData("10.0.0.5", null, false)]
        [InlineData("127.0.0.1", "203.0.113.9", false)]
        public void The_hangfire_dashboard_answers_only_on_the_server_itself(string remote, string? forwardedFor, bool allowed)
        {
            var http = new DefaultHttpContext();
            http.Connection.RemoteIpAddress = IPAddress.Parse(remote);
            if (forwardedFor is not null)
            {
                http.Request.Headers["X-Forwarded-For"] = forwardedFor;
            }
            Assert.Equal(allowed, HangfireAuthorizationFilter.IsLocalRequest(http));
        }
    }
}
