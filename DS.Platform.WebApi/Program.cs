using Asp.Versioning;
using DS.Platform.Application.Configurations;
using DS.Platform.Application.Extensions;
using DS.Platform.Infrastructure.Persistence.Extensions;
using DS.Platform.Infrastructure.Persistence.Services.BackgroudServices;
using DS.Platform.WebApi.Extensions;
using DS.Platform.WebApi.Filters;
using DS.Platform.WebApi.Managers.Preferences;
using DS.Platform.WebApi.Middlewares;
using Hangfire;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Serilog;
using System;
using System.Linq;

try
{
    // Archivos subidos (imágenes de productos, logos, fotos de perfil) van a wwwroot. ASP.NET solo sirve esa carpeta si existe
    // al arrancar: en una instalación nueva se crea aquí para que lo subido se vea sin reiniciar la API.
    System.IO.Directory.CreateDirectory(System.IO.Path.Combine(System.IO.Directory.GetCurrentDirectory(), "wwwroot"));

    // Logger de arranque (consola) hasta leer la configuración; después manda la sección "Serilog" de appsettings
    // (consola + archivo diario en Logs/, 30 días). En Docker la consola la recoge `docker compose logs`.
    Log.Logger = new LoggerConfiguration().WriteTo.Console().CreateBootstrapLogger();

    var builder = WebApplication.CreateBuilder(args);
    var _configuration = builder.Configuration;
    builder.Host.UseSerilog((context, services, logger) => logger
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services));
    SecretsExtensions.ValidateRequiredSecrets(_configuration);

    var Cors = "Cors";

      
    builder.Services.AddHttpClient();
    builder.Services.AddLicenseRateLimiting();

  

    builder.Services.AddForwarding(_configuration);
    builder.Services.AddCurrentUserService();
    builder.Services.AddSerialization();
     builder.Services.AddDatabase(_configuration);
    builder.Services.AddServerStorage(); 
    builder.Services.AddScoped<ServerPreferenceManager>();
    
    builder.Services.AddServerLocalization();
    builder.Services.AddErpDataProtection(_configuration, builder.Environment);
    builder.Services.AddIdentity();
    builder.Services.AddJwtAuthentication(builder.Services.GetApplicationSettings(_configuration));
    builder.Services.AddSignalR();
    builder.Services.AddHttpContextAccessor();
    builder.Services.AddApplicationLayer();
    builder.Services.AddApplicationServices();
    builder.Services.AddRepositories();
    builder.Services.AddSharedInfrastructure(_configuration);
      builder.Services.AddMemoryCache();

    builder.Services.AddInfrastructureMappings();
   
    builder.Services.ConfigureHangfireServices(_configuration);


    builder.Services.AddLogging();


    builder.Services.AddControllers().AddValidators();
    builder.Services.AddRazorPages();
    builder.Services.AddApiVersioning(config =>
    {
        config.DefaultApiVersion = new ApiVersion(1, 0);
        config.AssumeDefaultVersionWhenUnspecified = true;
        config.ReportApiVersions = true;
    });
    builder.Services.AddLazyCache();

    builder.Services.AddEndpointsApiExplorer();
    builder.Services.RegisterSwagger();
    //builder.Services.AddOpenApi();

    // Orígenes del frontend (Cors:AllowedOrigins). En Docker el frontend y la API salen por el mismo nginx (mismo origen) y la
    // lista va vacía; en Development, sin configurar, es el de `ng serve`.
    // Una variable vacía (Cors__AllowedOrigins__0 sin valor, como en deploy/docker-compose.coolify.yml) no cuenta como origen.
    var allowedOrigins = (_configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [])
        .Where(origin => !string.IsNullOrWhiteSpace(origin)).ToArray();
    if (allowedOrigins.Length == 0 && builder.Environment.IsDevelopment())
    {
        allowedOrigins = ["https://localhost:4210"];
    }
    builder.Services.AddHealthChecks()
        .AddNpgSql(_configuration.GetConnectionString("DefaultConnection")!, name: "postgres");

    builder.Services.AddCors(options =>
    {
        options.AddPolicy(name: Cors,
            policy =>
            {
                policy.WithOrigins(allowedOrigins);
                policy.AllowAnyMethod();
                // Las descargas (exportaciones) llevan el nombre del archivo en esta cabecera.
                policy.WithExposedHeaders("Content-Disposition");
                policy.WithHeaders(
                    "Authorization",
                    "Content-Type");
                //policy.AllowCredentials(); // Solo si es necesario
            });
    });


    var app = builder.Build();


    app.UseCors(Cors);

    // Configure the HTTP request pipeline.
    if (app.Environment.IsDevelopment())
    {
        //app.MapOpenApi();
        app.UseSwagger();
        app.UseSwaggerUI();
        app.ConfigureSwagger(); // Move inside dev check or ensure it handles prod correctly
    }
    
    app.UseForwarding(_configuration);
    app.UseHttpsRedirection();
    // Fuera del manejador de errores: registra el código que recibe el cliente (400, 404…), no la excepción original.
    app.UseSerilogRequestLogging();
    app.UseMiddleware<ErrorHandlerMiddleware>();
    app.UseStaticFiles();

    var options = app.Services.GetRequiredService<IOptions<RequestLocalizationOptions>>();
    app.UseRequestLocalization(options.Value);
    //  app.UseRequestLocalizationByCulture();
    app.UseRouting();
    app.UseRateLimiter();

    app.UseAuthentication();
    app.UseAuthorization();

    await app.UseCustomCultureConfiguration();

    app.MapControllers();
    // Salud para Docker / el balanceador: solo "Healthy" o "Unhealthy" (sin detalles), sin login.
    app.MapHealthChecks("/health").AllowAnonymous();

    // Panel de Hangfire: solo en Development, o si se habilita a propósito (Hangfire:DashboardEnabled), y siempre solo desde
    // el mismo servidor (HangfireAuthorizationFilter). Muestra los argumentos de los jobs: nunca se abre a la red.
    if (app.Environment.IsDevelopment() || _configuration.GetValue<bool>("Hangfire:DashboardEnabled"))
    {
        app.UseHangfireDashboard("/jobs", new DashboardOptions
        {
            DashboardTitle = "Consola de plataforma",
            Authorization = new[] { new HangfireAuthorizationFilter() }
        });
    }
    // app.MapHub<SignalRHub>("/signalRHub");
    // app.UseEndpoints();
    // Inicializa el almacenamiento de Hangfire (JobStorage.Current) para los jobs programados y BackgroundJob.Enqueue. Antes lo
    // hacía el panel /jobs de paso; fuera de Development el panel no se monta y sin esto la API no arranca.
    _ = app.Services.GetRequiredService<JobStorage>();
    using (var scope = app.Services.CreateScope())
    {
        var services = scope.ServiceProvider;
        var hangfireCsvService = services.GetRequiredService<HangfireBackupAuditTrailsJobService>();
        hangfireCsvService.ScheduleBackupAuditTrailsJob();
        services.GetRequiredService<HangfireSubscriptionsJobService>().Schedule();
        services.GetRequiredService<HangfireRemoteActionsJobService>().Schedule();
        services.GetRequiredService<HangfireNotificationsJobService>().Schedule();
        services.GetRequiredService<HangfireDeploymentsJobService>().Schedule();
        services.GetRequiredService<HangfirePortalAccessJobService>().Schedule();
        services.GetRequiredService<HangfireApiCredentialsJobService>().Schedule();
    }
    
    app.Initialize(_configuration);

    app.Run();


}

catch (Exception ex)
{
    var type = ex.GetType().Name;
    // Las herramientas de EF (migraciones) detienen el host a propósito: no es un error.
    if (type.Equals("StopTheHostException", StringComparison.Ordinal) || type.Equals("HostAbortedException", StringComparison.Ordinal))
    {
        throw;
    }

    Log.Fatal(ex, "Unhandled exception");
    // Código de salida distinto de 0: Docker / el servicio ven que la API no arrancó (antes salía con 0).
    Environment.ExitCode = 1;
    Console.WriteLine($"CreateBuilder error: {ex.Message}");
    if (ex is AggregateException ae)
    {
        foreach (var inner in ae.InnerExceptions)
        {
             Console.WriteLine($"Inner Exception: {inner.Message}");
             Console.WriteLine($"Inner StackTrace: {inner.StackTrace}");
        }
    }
    else
    {
         Console.WriteLine($"Exception: {ex.Message}");
         if(ex.InnerException != null)
         {
             Console.WriteLine($"Inner Exception: {ex.InnerException.Message}");
         }
    }
}
finally
{
    Log.CloseAndFlush();
}
