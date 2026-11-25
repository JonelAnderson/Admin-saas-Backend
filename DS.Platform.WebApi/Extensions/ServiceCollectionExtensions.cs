using Microsoft.Extensions.Caching.Memory;
using DS.Platform.Application.Configurations;
using DS.Platform.Application.Interfaces.Serialization.Options;
using DS.Platform.Application.Interfaces.Serialization.Serializers;
using DS.Platform.Application.Interfaces.Serialization.Settings;
using DS.Platform.Application.Interfaces.Services;
using DS.Platform.Application.Interfaces.Services.Account;
using DS.Platform.Application.Interfaces.Services.Identity;
using DS.Platform.Application.Serialization.JsonConverters;
using DS.Platform.Application.Serialization.Options;
using DS.Platform.Application.Serialization.Serializers;
using DS.Platform.Application.Serialization.Settings;
using DS.Platform.Domain.Entities.Identity;
using DS.Platform.Infrastructure.Persistence.Contexts;
using DS.Platform.Infrastructure.Persistence.Seed;
using DS.Platform.Infrastructure.Persistence.Services;
using DS.Platform.Infrastructure.Persistence.Services.BackgroudServices;
using DS.Platform.WebApi.Services;
using DS.Platform.Infrastructure.Services;
using DS.Platform.Infrastructure.Services.Identity;
using DS.Platform.Infrastructure.Shared.Services;
using DS.Platform.Infrastructure.Shared.Services.ApiSunat;
using DS.Platform.Infrastructure.Shared.Services.Coolify;
using DS.Platform.Application.Interfaces.Services.Coolify;
using DS.Platform.Application.Interfaces.Services.Portal;
using DS.Platform.Infrastructure.Shared.Services.Portal;
using DS.Platform.Infrastructure.Shared.Services.Licensing;
using DS.Platform.Application.Interfaces.Services.Licensing;
using DS.Platform.Application.Interfaces.Services.ApiSunat;
using DS.Platform.Shared.Constants.Application;
using DS.Platform.Shared.Constants.Localization;
using DS.Platform.Shared.Wrapper;
using DS.Platform.WebApi.Authorization;
using DS.Platform.WebApi.Managers.Preferences;
using DS.Platform.WebApi.Settings;
using Hangfire;
using Hangfire.PostgreSql;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Localization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using Newtonsoft.Json;
using Serilog;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;

namespace DS.Platform.WebApi.Extensions
{
    internal static class ServiceCollectionExtensions
    {
     

        public static async Task UseCustomCultureConfiguration(this IApplicationBuilder app)
        {
            using var scope = app.ApplicationServices.CreateScope();
            await SetCultureFromServerPreferenceAsync(scope.ServiceProvider);
        }


        public static void ConfigureHangfireServices(this IServiceCollection services, IConfiguration configuration)
        {
            var hangfireConnection = configuration.GetConnectionString("DefaultConnection");
            services.AddHangfire(x => x.UsePostgreSqlStorage(options =>
                options.UseNpgsqlConnection(hangfireConnection!)));

            services.AddHangfireServer();

        }

        private const string TokenExpiredItem = "erp.token-expired";

        internal static IServiceCollection AddForwarding(this IServiceCollection services, IConfiguration configuration)
        {
            var applicationSettingsConfiguration = configuration.GetSection(nameof(AppConfiguration));
            var config = applicationSettingsConfiguration.Get<AppConfiguration>(); 
            if (config!.BehindSSLProxy)
            {
                services.Configure<ForwardedHeadersOptions>(options =>
                {
                    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
                    // ProxyIP: una IP (172.18.0.5), una red (172.16.0.0/12, la red interna de Docker) o varias separadas por
                    // coma. La API solo debe ser alcanzable por el proxy: nunca publicar su puerto si se confía en una red.
                    foreach (var entry in (config.ProxyIP ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                    {
                        if (System.Net.IPNetwork.TryParse(entry, out var network))
                            options.KnownIPNetworks.Add(network);
                        else if (IPAddress.TryParse(entry, out var proxyIP))
                            options.KnownProxies.Add(proxyIP);
                        else
                            Log.Logger.Warning("Invalid Proxy IP of {IpCheck}, Not Loaded", entry);
                    }
                    // Varios proxies en cadena (Caddy con TLS -> nginx -> API): recorre los saltos de confianza hasta el
                    // primero que no lo es, que es la IP real del cliente.
                    options.ForwardLimit = null;
                });

            
            }
            
            return services;
        }






        public static IServiceCollection AddServerLocalization(this IServiceCollection services)
        {
            services.AddLocalization();
            services.Configure<RequestLocalizationOptions>(options =>
            {
                var supportedCultures = LocalizationConstants.SupportedLanguages.Select(x => new CultureInfo(x.Code)).ToList();
                options.DefaultRequestCulture = new RequestCulture("es-ES");
                options.SupportedCultures = supportedCultures;
                options.SupportedUICultures = supportedCultures;
            });
            return services;
        }

        public static async Task SetCultureFromServerPreferenceAsync(IServiceProvider serviceProvider)
        {
            var storageService = serviceProvider.GetService<ServerPreferenceManager>();
            if (storageService != null)
            {
                // TODO - should implement ServerStorageProvider to work correctly!
                CultureInfo culture;
                if (await storageService.GetPreference() is ServerPreference preference)
                    culture = new(preference.LanguageCode);
                else
                    culture = new(LocalizationConstants.SupportedLanguages.FirstOrDefault()?.Code ?? "es-ES");

                culture.DateTimeFormat.ShortDatePattern = "dd/MM/yyyy";
                culture.DateTimeFormat.LongTimePattern = "HH:mm:ss";
                culture.DateTimeFormat.ShortTimePattern = "HH:mm:ss";
                culture.DateTimeFormat.FullDateTimePattern = "dd/MM/yyyy HH:mm:ss";
                culture.DateTimeFormat.DateSeparator = "/";
                culture.DateTimeFormat.TimeSeparator = ":";

                CultureInfo.DefaultThreadCurrentCulture = culture;
                CultureInfo.DefaultThreadCurrentUICulture = culture;
                CultureInfo.CurrentCulture = culture;
                CultureInfo.CurrentUICulture = culture;
            }
        }
       


        internal static AppConfiguration GetApplicationSettings(
           this IServiceCollection services,
           IConfiguration configuration)
        {
            var applicationSettingsConfiguration = configuration.GetSection(nameof(AppConfiguration));
            services.Configure<AppConfiguration>(applicationSettingsConfiguration);
            return applicationSettingsConfiguration.Get<AppConfiguration>()!;
        }

        internal static void RegisterSwagger(this IServiceCollection services)
        {
            services.AddSwaggerGen(c =>
            {
                // /// comments of controllers (GenerateDocumentationFile in the csproj).
                var xmlPath = System.IO.Path.Combine(AppContext.BaseDirectory, $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml");
                if (System.IO.File.Exists(xmlPath))
                {
                    c.IncludeXmlComments(xmlPath);
                }

                c.SwaggerDoc("v1", new OpenApiInfo
                {

                    Version = "v1",
                    Title = "Consola de plataforma",
                    Description = "Gestión del SaaS del ERP y de la API SUNAT",
                    TermsOfService = new Uri("https://opensource.org/licenses/NIT"),
                    Contact = new OpenApiContact
                    {
                        Name = "DS-TM SYSTEM S.R.LTDA.",
                        Email = "dstmperu@gmail.com",
                        Url = new Uri("https://devperu.com.pe")
                    },
                    License = new OpenApiLicense
                    {
                        Name = "MIT License",
                        Url = new Uri("https://opensource.org/licenses/")
                    }
                });


                c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
                {
                    Name = "Authorization",
                    In = ParameterLocation.Header,
                    Type = SecuritySchemeType.ApiKey,
                    Scheme = "Bearer",
                    BearerFormat = "JWT",
                    Description = "Input your Bearer token in this format - Bearer {your token here} to access this API",
                });
                c.AddSecurityRequirement(document => new OpenApiSecurityRequirement
                {
                    {
                        new OpenApiSecuritySchemeReference("Bearer", document), new List<string>()
                    },
                });
            });
        }

        internal static IServiceCollection AddSerialization(this IServiceCollection services)
        {
            services
                .AddScoped<IJsonSerializerOptions, SystemTextJsonOptions>()
                .Configure<SystemTextJsonOptions>(configureOptions =>
                {
                    if (!configureOptions.JsonSerializerOptions.Converters.Any(c => c.GetType() == typeof(TimespanJsonConverter)))
                        configureOptions.JsonSerializerOptions.Converters.Add(new TimespanJsonConverter());
                });
            services.AddScoped<IJsonSerializerSettings, NewtonsoftJsonSettings>();

            services.AddScoped<IJsonSerializer, SystemTextJsonSerializer>(); // you can change it
            return services;
        }

        internal static IServiceCollection AddDatabase(this IServiceCollection services,
        IConfiguration configuration)
        {
            // Text filters of the lists use ILIKE (case-insensitive) on PostgreSQL: see Repositories/Common/TextSearch.
            DS.Platform.Infrastructure.Persistence.Repositories.Common.TextSearch.UsePostgresILike = true;
            services
                .AddDbContext<ApplicationDbContext>(options => options
                    .UseNpgsql(configuration.GetConnectionString("DefaultConnection")))
                .AddTransient<IDatabaseSeeder, DatabaseSeeder>();

            return services;
        }


        internal static IServiceCollection AddCurrentUserService(this IServiceCollection services)
        {
            services.AddHttpContextAccessor();
            services.AddScoped<ICurrentUserService, CurrentUserService>();
           
            return services;
        }

        internal static IServiceCollection AddIdentity(this IServiceCollection services)
        {
            // Fase 12a: contraseña 8-16 con mayúscula / minúscula / número / especial, bloqueo 5 fallos → 15 min,
            // store con la clave del autenticador cifrada y los códigos de recuperación con hash.
            services
                .AddIdentity<AdmUser, AdmRole>(ErpIdentitySetup.Configure)
                .AddEntityFrameworkStores<ApplicationDbContext>()
                .AddErpIdentityHardening()
                .AddDefaultTokenProviders();

           

            return services;
        }

        internal static IServiceCollection AddSharedInfrastructure(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddTransient<IDateTimeService, DateTimeService>();
            // MailSettings usa los nombres de appsettings, user-secrets y Deploy (EmailFrom, SmtpHost, SmtpPort, SmtpUser, SmtpPass);
            // también acepta los de la clase (From, Host...). Antes no se leía ninguno y el correo nunca salía.
            services.Configure<MailConfiguration>(mail =>
            {
                var section = configuration.GetSection("MailSettings");
                string? Read(params string[] keys) => keys.Select(k => section[k]).FirstOrDefault(v => !string.IsNullOrWhiteSpace(v))?.Trim();
                mail.From = Read("EmailFrom", "From");
                mail.Host = Read("SmtpHost", "Host");
                mail.Port = int.TryParse(Read("SmtpPort", "Port"), out var port) ? port : 587;
                mail.UserName = Read("SmtpUser", "UserName");
                mail.Password = Read("SmtpPass", "Password");
                mail.DisplayName = Read("DisplayName") ?? "Consola de plataforma";
            });
            services.AddTransient<IMailService, SMTPMailService>();

            // API SUNAT (/api/admin/**): BaseUrl en appsettings; la clave de servicio (ApiSunat__AdminKey) solo en user-secrets / entorno.
            // Sin redirecciones automáticas: la clave nunca viaja a otra dirección.
            services.Configure<ApiSunatSettings>(configuration.GetSection(ApiSunatSettings.SectionName));
            // Lo no secreto de ApiSunat (dirección pública https que se entrega al ERP con sus credenciales, fase C6b).
            services.AddSingleton<IApiSunatSettings, ApiSunatPublicSettings>();
            // Firma de licencias (Ed25519): la llave privada License__SigningKey solo en user-secrets / entorno.
            services.Configure<LicenseSettings>(configuration.GetSection(LicenseSettings.SectionName));
            services.AddSingleton<ILicenseSigner, Ed25519LicenseSigner>();
            services.AddHttpClient<IApiSunatAdminClient, ApiSunatAdminClient>()
                .ConfigurePrimaryHttpMessageHandler(() => new System.Net.Http.HttpClientHandler { AllowAutoRedirect = false });

            // Coolify (fase C5): BaseUrl / ProjectUuid / ServerUuid en appsettings o entorno; el token (Coolify__Token) solo en user-secrets /
            // entorno. Sin redirecciones automáticas: el token nunca viaja a otra dirección.
            services.Configure<CoolifySettings>(configuration.GetSection(CoolifySettings.SectionName));
            services.Configure<ProvisioningOptions>(configuration.GetSection(ProvisioningOptions.SectionName));
            services.AddSingleton<IProvisioningSettings, ProvisioningSettings>();
            services.AddSingleton<IProvisioningSecretsGenerator, ProvisioningSecretsGenerator>();

            // Portal de clientes de la API SUNAT (fase C6): solo su dirección pública (https), de donde sale el enlace de activación.
            services.Configure<PortalOptions>(configuration.GetSection(PortalOptions.SectionName));
            services.AddSingleton<IPortalSettings, PortalSettings>();
            services.AddHttpClient<ICoolifyClient, CoolifyClient>()
                .ConfigurePrimaryHttpMessageHandler(() => new System.Net.Http.HttpClientHandler { AllowAutoRedirect = false });
           
            return services;
        }

        internal static IServiceCollection AddApplicationServices(this IServiceCollection services)
        {
           
            services.AddTransient<IRoleClaimService, RoleClaimService>();
            services.AddTransient<ITokenService, IdentityService>();
            // Revocación de sesiones: sello del JWT contra el security stamp vigente (caché de 30 s por usuario).
            services.AddSingleton<ISessionStampService, SessionStampService>();
            services.AddSingleton<DS.Platform.Application.Interfaces.Services.Identity.IUserPermissionCache, UserPermissionCache>();
            services.AddTransient<IMfaService, MfaService>();
            services.AddTransient<ITrustedDeviceService, TrustedDeviceService>();
            services.AddSingleton<MfaTicketProtector>();
            services.AddTransient<IRoleService, RoleService>();
            services.AddTransient<IAccountService, AccountService>();
            services.AddTransient<IUserService, UserService>();
            services.AddTransient<IFileStorageLocal, FileStorageLocal>();
            services.AddTransient<IUploadService, UploadService>();
            services.AddTransient<BackupAuditTrailsJobService>();
            services.AddTransient<HangfireBackupAuditTrailsJobService>();
            // Diario: recalcula el estado de las suscripciones (solo envía su command).
            services.AddTransient<HangfireSubscriptionsJobService>();
            // Cada minuto: envía a la API SUNAT las acciones remotas pendientes (solo envía su command).
            services.AddTransient<HangfireRemoteActionsJobService>();
            // Cada 5 min envía los avisos por correo pendientes; a diario encola los de certificados por vencer (solo envían su command).
            services.AddTransient<HangfireNotificationsJobService>();
            // Cada minuto: ejecuta en Coolify los despliegues de instancias pendientes (solo envía su command).
            services.AddTransient<HangfireDeploymentsJobService>();
            // Cada minuto: cumple en la API SUNAT los accesos al portal de clientes pendientes (solo envía su command).
            services.AddTransient<HangfirePortalAccessJobService>();
            // Cada minuto: pide a la API SUNAT las credenciales de las entregas al ERP pendientes y borra el secreto de las vencidas (solo envía su command).
            services.AddTransient<HangfireApiCredentialsJobService>();

            return services;
        }

        internal static IServiceCollection AddJwtAuthentication(
            this IServiceCollection services, AppConfiguration config)
        {
            var key = Encoding.UTF8.GetBytes(config.Secret!);
            services
                .AddAuthentication(authentication =>
                {
                    authentication.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                    authentication.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
                })
                .AddJwtBearer(async bearer =>
                {
                    bearer.RequireHttpsMetadata = false;
                    bearer.SaveToken = true;
                    bearer.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuerSigningKey = true,
                        IssuerSigningKey = new SymmetricSecurityKey(key),
                        ValidateIssuer = false,
                        ValidateAudience = false,
                        RoleClaimType = ClaimTypes.Role,
                        ClockSkew = TimeSpan.Zero
                    };


                    bearer.Events = new JwtBearerEvents
                    {
                        OnMessageReceived = context =>
                        {
                            var accessToken = context.Request.Query["access_token"];

                            // If the request is for our hub...
                            var path = context.HttpContext.Request.Path;
                            if (!string.IsNullOrEmpty(accessToken) &&
                                (path.StartsWithSegments(ApplicationConstants.SignalR.HubUrl)))
                            {
                                // Read the token out of the query string
                                context.Token = accessToken;
                            }
                            return Task.CompletedTask;
                        },
                        // The JWT carries roles only: add the user's permission claims (same 5-minute cache as
                        // PermissionAuthorizationHandler) so ICurrentUserService.HasPermission works inside handlers.
                        OnTokenValidated = async context =>
                        {
                            var userId = context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
                            if (string.IsNullOrEmpty(userId) || context.Principal is null)
                            {
                                context.Fail("Token sin usuario.");
                                return;
                            }
                            // Revocación: el sello de sesión del token debe ser el vigente (logout, contraseña, MFA, roles o
                            // cierre forzado lo cambian) y el usuario debe seguir activo. Si no, 401 y el frontend vuelve al login.
                            var sessions = context.HttpContext.RequestServices.GetRequiredService<DS.Platform.Application.Interfaces.Services.Identity.ISessionStampService>();
                            var stamp = context.Principal.FindFirstValue(DS.Platform.Infrastructure.Services.Identity.SessionStampService.ClaimType);
                            if (!await sessions.IsCurrentAsync(userId, stamp, context.HttpContext.RequestAborted))
                            {
                                context.Fail("Sesión cerrada o revocada.");
                                return;
                            }
                            var cache = context.HttpContext.RequestServices.GetRequiredService<DS.Platform.Application.Interfaces.Services.Identity.IUserPermissionCache>();
                            var permissions = await cache.GetAsync(userId, async () =>
                            {
                                var tokens = context.HttpContext.RequestServices.GetRequiredService<DS.Platform.Application.Interfaces.Services.Identity.ITokenService>();
                                var result = await tokens.GetPermissionsAsync(userId);
                                return result.Succeeded ? result.Data : null;
                            });

                            context.Principal.AddIdentity(new ClaimsIdentity(
                                permissions.Select(p => new Claim(DS.Platform.Shared.Constants.Permission.ApplicationClaimTypes.Permission, p))));
                        },
                        OnAuthenticationFailed = c =>
                        {
                            // Token vencido, mal formado o firmado con otra clave: el usuario queda anónimo y, si el endpoint
                            // pide login, OnChallenge responde 401 (el frontend renueva o vuelve al login). Antes respondía 500
                            // (y en DEBUG el texto de la excepción), también en endpoints anónimos como el login.
                            if (c.Exception is SecurityTokenExpiredException)
                            {
                                c.HttpContext.Items[TokenExpiredItem] = true;
                            }
                            return Task.CompletedTask;
                        },
                        OnChallenge = context =>
                        {
                            context.HandleResponse();
                            if (!context.Response.HasStarted)
                            {
                                context.Response.StatusCode = (int)HttpStatusCode.Unauthorized;
                                context.Response.ContentType = "application/json";
                                var message = context.HttpContext.Items.ContainsKey(TokenExpiredItem) ? "The Token is expired." : "You are not Authorized.";
                                var result = JsonConvert.SerializeObject(Result.Fail(message));
                                return context.Response.WriteAsync(result);
                            }

                            return Task.CompletedTask;
                        },
                        OnForbidden = context =>
                        {
                            context.Response.StatusCode = (int)HttpStatusCode.Forbidden;
                            context.Response.ContentType = "application/json";
                            var result = JsonConvert.SerializeObject(Result.Fail("You are not authorized to access this resource."));
                            return context.Response.WriteAsync(result);
                        },
                    };
                });

            // Todo endpoint sin [Authorize] ni [AllowAnonymous] exige un usuario autenticado (AuthorizationDefaults).
            services.AddAuthorization(AuthorizationDefaults.Configure);
            services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
            services.AddScoped<IAuthorizationHandler, PermissionAuthorizationHandler>();

            return services;
        }
    }
}