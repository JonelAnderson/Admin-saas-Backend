using DS.Platform.Application.Features.Credentials.Commands.Dispatch;
using Hangfire;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DS.Platform.Infrastructure.Persistence.Services.BackgroudServices
{
    /// <summary>
    /// Cada minuto: pide a la API SUNAT las credenciales de las entregas al ERP que tocan y borra el secreto de las que vencieron
    /// (fase C6b). Solo envía <see cref="DispatchApiCredentialDeliveriesCommand"/>; la lógica, los reintentos y el control de
    /// concurrencia viven en su handler.
    /// </summary>
    public class HangfireApiCredentialsJobService
    {
        public const string JobId = "ApiCredentialsDispatch";

        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<HangfireApiCredentialsJobService> _logger;

        public HangfireApiCredentialsJobService(IServiceProvider serviceProvider, ILogger<HangfireApiCredentialsJobService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        public void Schedule()
            => RecurringJob.AddOrUpdate(JobId, () => RunAsync(CancellationToken.None), Cron.Minutely());

        public async Task RunAsync(CancellationToken cancellationToken)
        {
            using var scope = _serviceProvider.CreateScope();
            var result = await scope.ServiceProvider.GetRequiredService<IMediator>().Send(new DispatchApiCredentialDeliveriesCommand(), cancellationToken);
            if (result.Data > 0)
            {
                _logger.LogInformation("Credenciales: {Ready} entregas listas para que el ERP las recoja.", result.Data);
            }
        }
    }
}
