using DS.Platform.Application.Behaviours;
using DS.Platform.Application.Helpers;
using DS.Platform.Application.Interfaces;
using DS.Platform.Application.Validators.Features.Files;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;

namespace DS.Platform.Application.Extensions
{
    public static class ServiceExtensions
    {
        public static void AddApplicationLayer(this IServiceCollection services)
        {
            services.AddValidatorsFromAssembly(Assembly.GetExecutingAssembly());

            services.AddMediatR(cfg =>
            {
                cfg.RegisterServicesFromAssemblies(Assembly.GetExecutingAssembly());
                cfg.AddOpenBehavior(typeof(ValidationBehavior<,>));
            });
            services.AddScoped<IModelHelper, ModelHelper>();
            services.AddScoped<IFileUploadValid, FileUploadValid>();

            // Motores compartidos de la consola (si un módulo los necesita) se registran aquí: uno por petición, sobre el
            // Unit of Work de la petición, y nunca hacen Commit.
        }
    }
}
