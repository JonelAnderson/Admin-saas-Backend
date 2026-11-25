using DS.Platform.Application.Features.Portal.Commands.Dispatch;
using Hangfire;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DS.Platform.Infrastructure.Persistence.Services.BackgroudServices
{
    /// <summary>
    /// Cada minuto: cumple en la API SUNAT los accesos al portal de clientes que tocan (invitar, habilitar y deshabilitar, fase C6).
    /// Solo envía <see cref="DispatchPortalAccessesCommand"/>; la lógica, los reintentos y el control de concurrencia viven en su handler.
    /// </summary>
    public class HangfirePortalAccessJobService
    {
        public const string JobId = "PortalAccessDispatch";

        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<HangfirePortalAccessJobService> _logger;

        public HangfirePortalAccessJobService(IServiceProvider serviceProvider, ILogger<HangfirePortalAccessJobService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        public void Schedule()
            => RecurringJob.AddOrUpdate(JobId, () => RunAsync(CancellationToken.None), Cron.Minutely());

        public async Task RunAsync(CancellationToken cancellationToken)
        {
            using var scope = _serviceProvider.CreateScope();
            var result = await scope.ServiceProvider.GetRequiredService<IMediator>().Send(new DispatchPortalAccessesCommand(), cancellationToken);
            if (result.Data > 0)
            {
                _logger.LogInformation("Portal: {Done} accesos cumplidos en la API SUNAT.", result.Data);
            }
        }
    }
}
