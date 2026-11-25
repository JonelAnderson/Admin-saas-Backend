using DS.Platform.Application.Features.RemoteActions.Commands.Dispatch;
using Hangfire;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DS.Platform.Infrastructure.Persistence.Services.BackgroudServices
{
    /// <summary>
    /// Cada minuto: envía a la API SUNAT las acciones remotas que toca (suspender / reactivar la emisión). Solo envía
    /// <see cref="DispatchRemoteActionsCommand"/>; la lógica, los reintentos y el control de concurrencia viven en su handler.
    /// </summary>
    public class HangfireRemoteActionsJobService
    {
        public const string JobId = "RemoteActionsDispatch";

        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<HangfireRemoteActionsJobService> _logger;

        public HangfireRemoteActionsJobService(IServiceProvider serviceProvider, ILogger<HangfireRemoteActionsJobService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        public void Schedule()
            => RecurringJob.AddOrUpdate(JobId, () => RunAsync(CancellationToken.None), Cron.Minutely());

        public async Task RunAsync(CancellationToken cancellationToken)
        {
            using var scope = _serviceProvider.CreateScope();
            var result = await scope.ServiceProvider.GetRequiredService<IMediator>().Send(new DispatchRemoteActionsCommand(), cancellationToken);
            if (result.Data > 0)
            {
                _logger.LogInformation("Acciones remotas: {Done} aplicadas en la API SUNAT.", result.Data);
            }
        }
    }
}
