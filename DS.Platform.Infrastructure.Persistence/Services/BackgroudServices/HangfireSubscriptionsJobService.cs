using DS.Platform.Application.Features.Subscriptions.Commands.Evaluate;
using Hangfire;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DS.Platform.Infrastructure.Persistence.Services.BackgroudServices
{
    /// <summary>
    /// Diario (06:00 Lima = 11:00 UTC): recalcula el estado de las suscripciones (vencidas → En gracia → Suspendida). Solo envía
    /// <see cref="EvaluateSubscriptionsCommand"/>; la lógica vive en su handler.
    /// </summary>
    public class HangfireSubscriptionsJobService
    {
        public const string JobId = "SubscriptionsDaily";

        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<HangfireSubscriptionsJobService> _logger;

        public HangfireSubscriptionsJobService(IServiceProvider serviceProvider, ILogger<HangfireSubscriptionsJobService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        public void Schedule()
            => RecurringJob.AddOrUpdate(JobId, () => RunAsync(CancellationToken.None), "0 11 * * *");

        public async Task RunAsync(CancellationToken cancellationToken)
        {
            using var scope = _serviceProvider.CreateScope();
            var result = await scope.ServiceProvider.GetRequiredService<IMediator>().Send(new EvaluateSubscriptionsCommand(), cancellationToken);
            _logger.LogInformation("Suscripciones: {Changed} cambiaron de estado.", result.Data);
        }
    }
}
