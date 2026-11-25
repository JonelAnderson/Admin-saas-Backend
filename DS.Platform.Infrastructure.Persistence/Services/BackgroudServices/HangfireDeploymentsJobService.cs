using DS.Platform.Application.Features.Deployments.Commands.Dispatch;
using Hangfire;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DS.Platform.Infrastructure.Persistence.Services.BackgroudServices
{
    /// <summary>
    /// Cada minuto: ejecuta en Coolify los despliegues de instancias que tocan (montar y actualizar, fase C5). Solo envía
    /// <see cref="DispatchDeploymentsCommand"/>; la lógica, los reintentos y el control de concurrencia viven en su handler.
    /// </summary>
    public class HangfireDeploymentsJobService
    {
        public const string JobId = "DeploymentsDispatch";

        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<HangfireDeploymentsJobService> _logger;

        public HangfireDeploymentsJobService(IServiceProvider serviceProvider, ILogger<HangfireDeploymentsJobService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        public void Schedule()
            => RecurringJob.AddOrUpdate(JobId, () => RunAsync(CancellationToken.None), Cron.Minutely());

        public async Task RunAsync(CancellationToken cancellationToken)
        {
            using var scope = _serviceProvider.CreateScope();
            var result = await scope.ServiceProvider.GetRequiredService<IMediator>().Send(new DispatchDeploymentsCommand(), cancellationToken);
            if (result.Data > 0)
            {
                _logger.LogInformation("Despliegues: {Done} terminados en Coolify.", result.Data);
            }
        }
    }
}
