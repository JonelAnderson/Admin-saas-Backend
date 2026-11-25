using DS.Platform.Application.Interfaces.Repositories;
using DS.Platform.Application.Interfaces.Serialization.Serializers;
using DS.Platform.Application.Interfaces.Services.Security;
using DS.Platform.Application.Interfaces.Services.Storage;
using DS.Platform.Application.Interfaces.Services.Storage.Provider;
using DS.Platform.Application.Serialization.JsonConverters;
using DS.Platform.Application.Serialization.Options;
using DS.Platform.Application.Serialization.Serializers;
using DS.Platform.Infrastructure.Persistence.Repositories;
using DS.Platform.Infrastructure.Persistence.Repository;
using DS.Platform.Infrastructure.Persistence.Services.Security;
using DS.Platform.Infrastructure.Repositories;
using DS.Platform.Infrastructure.Services.Storage;
using DS.Platform.Infrastructure.Services.Storage.Provider;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;

namespace DS.Platform.Infrastructure.Persistence.Extensions
{
    public static class ServiceRegistration
    {
        public static void AddInfrastructureMappings(this IServiceCollection services)
        {
        }

        public static IServiceCollection AddRepositories(this IServiceCollection services)
        {
            return services


                .AddTransient(typeof(IGenericRepositoryAsync<,>), typeof(GenericRepositoryAsync<,>))
                .AddTransient(typeof(IUnitOfWork<>), typeof(UnitOfWork<>))
                .AddSingleton<ISecretProtector, DataProtectionSecretProtector>();
                
        }
       

        public static IServiceCollection AddServerStorage(this IServiceCollection services)
          => AddServerStorage(services, null!);

        public static IServiceCollection AddServerStorage(this IServiceCollection services, Action<SystemTextJsonOptions> configure)
        {
            return services
                .AddScoped<IJsonSerializer, SystemTextJsonSerializer>()
                .AddScoped<IStorageProvider, ServerStorageProvider>()
                .AddScoped<IServerStorageService, ServerStorageService>()
                .AddScoped<ISyncServerStorageService, ServerStorageService>()
               
                .Configure<SystemTextJsonOptions>(configureOptions =>
                {
                    configure?.Invoke(configureOptions);
                    if (!configureOptions.JsonSerializerOptions.Converters.Any(c => c.GetType() == typeof(TimespanJsonConverter)))
                        configureOptions.JsonSerializerOptions.Converters.Add(new TimespanJsonConverter());
                });
        }



    }
}