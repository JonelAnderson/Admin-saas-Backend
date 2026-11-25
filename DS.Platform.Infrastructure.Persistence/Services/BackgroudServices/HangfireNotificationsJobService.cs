using DS.Platform.Application.Features.Notifications.Commands.QueueCertificateNotices;
using DS.Platform.Application.Features.Notifications.Commands.Send;
using Hangfire;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DS.Platform.Infrastructure.Persistence.Services.BackgroudServices
{
    /// <summary>
    /// Avisos por correo a los clientes (diseño §5.4). Cada 5 minutos envía los pendientes (<see cref="SendNotificationsCommand"/>) y
    /// cada día a las 08:00 de Lima (13:00 UTC) encola los de certificados por vencer (<see cref="QueueCertificateNoticesCommand"/>).
    /// Los avisos de cobro los encola <c>EvaluateSubscriptionsCommand</c> (job diario de suscripciones) y las constancias, el registro
    /// de pagos. Solo envía commands; la lógica vive en sus handlers.
    /// </summary>
    public class HangfireNotificationsJobService
    {
        public const string DispatchJobId = "NotificationsDispatch";
        public const string CertificatesJobId = "CertificateNoticesDaily";

        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<HangfireNotificationsJobService> _logger;

        public HangfireNotificationsJobService(IServiceProvider serviceProvider, ILogger<HangfireNotificationsJobService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        public void Schedule()
        {
            RecurringJob.AddOrUpdate(DispatchJobId, () => DispatchAsync(CancellationToken.None), "*/5 * * * *");
            RecurringJob.AddOrUpdate(CertificatesJobId, () => QueueCertificatesAsync(CancellationToken.None), "0 13 * * *");
        }

        public async Task DispatchAsync(CancellationToken cancellationToken)
        {
            using var scope = _serviceProvider.CreateScope();
            var result = await scope.ServiceProvider.GetRequiredService<IMediator>().Send(new SendNotificationsCommand(), cancellationToken);
            if (result.Data > 0)
            {
                _logger.LogInformation("Avisos: {Sent} enviados por correo.", result.Data);
            }
        }

        public async Task QueueCertificatesAsync(CancellationToken cancellationToken)
        {
            using var scope = _serviceProvider.CreateScope();
            var result = await scope.ServiceProvider.GetRequiredService<IMediator>().Send(new QueueCertificateNoticesCommand(), cancellationToken);
            if (result.Succeeded)
            {
                _logger.LogInformation("Avisos de certificados: {Queued} encolados.", result.Data);
            }
            else
            {
                _logger.LogWarning("Avisos de certificados: {Message}", string.Join(" ", result.Messages));
            }
        }
    }
}
