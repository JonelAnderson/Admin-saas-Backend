using DS.Platform.Application.Interfaces.Repositories;
using DS.Platform.Domain.Entities.Billing;
using DS.Platform.Domain.Entities.Customers;
using DS.Platform.Domain.Entities.Ops;
using DS.Platform.Domain.Enums;

namespace DS.Platform.Application.Features.Notifications.Common
{
    /// <summary>Un aviso por encolar: a qué cliente, de qué periodo (<see cref="DedupKey"/>) y qué dice. <c>ToEmail</c> reemplaza al correo de contacto del cliente (la invitación al portal va a la persona invitada).</summary>
    public sealed record NoticeSpec(
        NotificationType Type, Customer Customer, string DedupKey, string Subject, string Body,
        int? SubscriptionId = null, long? TenantId = null, Payment? Payment = null, string? ToEmail = null);

    /// <summary>
    /// Encola avisos por correo en la bandeja <c>ops.Notifications</c> (diseño §5.4). Como el resto de la bandeja, el caso de uso solo
    /// escribe en la base: el envío lo hace el job. No hace <c>Commit</c> (lo hace el caso de uso que llama, una sola vez). No repite:
    /// un aviso cuya clave ya existe se omite (también dentro de la misma lista). Un cliente sin correo deja el aviso Omitido, para
    /// que se vea en la bandeja y no se vuelva a intentar cada día.
    /// </summary>
    internal static class NotificationOutbox
    {
        public const string NoEmailMessage = "El cliente no tiene correo de contacto: registre uno para que reciba los avisos.";

        public static async Task<int> EnqueueAsync(
            IUnitOfWork<int> unitOfWork, IReadOnlyList<NoticeSpec> specs, DateTime nowUtc, CancellationToken cancellationToken)
        {
            if (specs.Count == 0)
            {
                return 0;
            }
            var existing = await unitOfWork.Notifications.GetExistingKeysAsync(specs.Select(s => s.DedupKey).ToList(), cancellationToken);
            var queued = 0;
            foreach (var spec in specs)
            {
                if (!existing.Add(spec.DedupKey))
                {
                    continue;
                }
                var address = string.IsNullOrWhiteSpace(spec.ToEmail) ? spec.Customer.ContactEmail : spec.ToEmail;
                var email = string.IsNullOrWhiteSpace(address) ? null : address.Trim();
                await unitOfWork.Notifications.AddAsync(new Notification
                {
                    Type = spec.Type,
                    CustomerId = spec.Customer.Id,
                    Customer = spec.Customer.Id == 0 ? spec.Customer : null,
                    SubscriptionId = spec.SubscriptionId,
                    TenantId = spec.TenantId,
                    Payment = spec.Payment,
                    DedupKey = spec.DedupKey,
                    ToEmail = email,
                    Subject = spec.Subject,
                    Body = spec.Body,
                    Status = email is null ? NotificationStatus.Skipped : NotificationStatus.Pending,
                    LastError = email is null ? NoEmailMessage : null,
                    NextAttemptOnUtc = nowUtc,
                    QueuedOnUtc = nowUtc,
                    IsActive = true
                });
                queued++;
            }
            return queued;
        }

        public static string ProductName(PlanProduct product) => product == PlanProduct.Erp ? "ERP" : "API SUNAT";
    }
}
