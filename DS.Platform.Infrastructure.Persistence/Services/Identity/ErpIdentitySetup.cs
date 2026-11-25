using Microsoft.AspNetCore.Identity;

namespace DS.Platform.Infrastructure.Services.Identity
{
    /// <summary>
    /// Opciones de Identity de la Fase 12a, compartidas por la WebApi y los tests:
    /// contraseña 8–16 con mayúscula, minúscula, número y especial; bloqueo de 15 minutos tras 5 fallos seguidos
    /// (contraseña o código MFA), también para usuarios nuevos; store con los secretos del MFA protegidos.
    /// </summary>
    public static class ErpIdentitySetup
    {
        public const int MaxFailedAccessAttempts = 5;
        public static readonly TimeSpan LockoutTimeSpan = TimeSpan.FromMinutes(15);

        public static void Configure(IdentityOptions options)
        {
            options.Password.RequiredLength = 8;
            options.Password.RequireDigit = true;
            options.Password.RequireLowercase = true;
            options.Password.RequireUppercase = true;
            options.Password.RequireNonAlphanumeric = true;
            options.User.RequireUniqueEmail = true;

            options.Lockout.MaxFailedAccessAttempts = MaxFailedAccessAttempts;
            options.Lockout.DefaultLockoutTimeSpan = LockoutTimeSpan;
            options.Lockout.AllowedForNewUsers = true;
        }

        /// <summary>Después de <c>AddEntityFrameworkStores</c>: reemplaza el store y agrega el máximo de 16 y los mensajes en español.</summary>
        public static IdentityBuilder AddErpIdentityHardening(this IdentityBuilder builder)
            => builder
                .AddUserStore<AdmUserStore>()
                .AddPasswordValidator<ErpPasswordValidator>()
                .AddErrorDescriber<ErpIdentityErrorDescriber>();
    }
}
