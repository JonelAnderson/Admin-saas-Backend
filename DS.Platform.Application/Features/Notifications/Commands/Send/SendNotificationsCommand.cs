using DS.Platform.Application.DTOs.Mail.Request;
using DS.Platform.Application.Features.Notifications.Common;
using DS.Platform.Application.Interfaces.Repositories;
using DS.Platform.Application.Interfaces.Services;
using DS.Platform.Domain.Entities.Ops;
using DS.Platform.Domain.Enums;
using DS.Platform.Shared.Wrapper;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace DS.Platform.Application.Features.Notifications.Commands.Send
{
    /// <summary>
    /// Envía los avisos por correo que toca (lo envía el job <c>NotificationsDispatch</c> cada 5 min). Igual que las acciones remotas:
    /// toma un lote y lo marca Enviando en un guardado propio (si otro despachador lo tomó, choca por <c>xmin</c> y no envía nada: un
    /// correo nunca sale dos veces), y después guarda el resultado de cada uno por separado. Sin servidor de correo configurado no toca
    /// nada: los avisos esperan (caducan a los 3 días). Devuelve cuántos se enviaron.
    /// </summary>
    public class SendNotificationsCommand : IRequest<Result<int>>
    {
        public int Batch { get; set; } = 20;
    }

    internal class SendNotificationsCommandHandler(
        IUnitOfWork<int> unitOfWork,
        IMailService mail,
        IDateTimeService dateTime) : IRequestHandler<SendNotificationsCommand, Result<int>>
    {
        public const string NotConfiguredMessage = "El correo no está configurado (MailSettings): los avisos esperan.";
        public const string ExpiredMessage = "Caducó sin poder enviarse (más de 3 días).";

        public async Task<Result<int>> Handle(SendNotificationsCommand request, CancellationToken cancellationToken)
        {
            if (!mail.IsConfigured)
            {
                return await Result<int>.SuccessAsync(0, NotConfiguredMessage);
            }

            var now = dateTime.NowUtc;
            var due = await unitOfWork.Notifications.GetDueAsync(now, Math.Clamp(request.Batch, 1, 100), cancellationToken);
            if (due.Count == 0)
            {
                return await Result<int>.SuccessAsync(0, "No hay avisos pendientes.");
            }

            foreach (var notification in due)
            {
                notification.Status = NotificationStatus.Sending;
                notification.LockedUntilUtc = now + NotificationRetryPolicy.Lease;
            }
            try
            {
                await unitOfWork.Commit(cancellationToken);
            }
            catch (DbUpdateConcurrencyException)
            {
                unitOfWork.ClearChanges();
                return await Result<int>.SuccessAsync(0, "Otro despachador tomó los avisos.");
            }

            var sent = 0;
            foreach (var notification in due)
            {
                if (await ProcessAsync(notification, cancellationToken))
                {
                    sent++;
                }
            }
            return await Result<int>.SuccessAsync(sent, $"{sent} avisos enviados.");
        }

        private async Task<bool> ProcessAsync(Notification notification, CancellationToken cancellationToken)
        {
            var delivered = false;
            notification.LockedUntilUtc = null;
            if (NotificationRetryPolicy.IsExpired(notification.QueuedOnUtc, dateTime.NowUtc))
            {
                notification.Status = NotificationStatus.Skipped;
                notification.LastError = ExpiredMessage;
            }
            else if (string.IsNullOrWhiteSpace(notification.ToEmail))
            {
                notification.Status = NotificationStatus.Skipped;
                notification.LastError = NotificationOutbox.NoEmailMessage;
            }
            else
            {
                notification.Attempts++;
                try
                {
                    await mail.SendAsync(new MailRequest { To = notification.ToEmail, Subject = notification.Subject, Body = notification.Body });
                    notification.Status = NotificationStatus.Sent;
                    notification.SentOnUtc = dateTime.NowUtc;
                    notification.LastError = null;
                    delivered = true;
                }
                catch (Exception ex)
                {
                    var error = ex.Message;
                    notification.LastError = error.Length > 1000 ? error[..1000] : error;
                    if (NotificationRetryPolicy.IsExhausted(notification.Attempts))
                    {
                        notification.Status = NotificationStatus.Abandoned;
                    }
                    else
                    {
                        notification.Status = NotificationStatus.Failed;
                        notification.NextAttemptOnUtc = dateTime.NowUtc + NotificationRetryPolicy.DelayAfter(notification.Attempts);
                    }
                }
            }

            // El cuerpo de la invitación al portal lleva el token de activación: una vez enviada (o descartada) no se conserva.
            if (notification.Type == NotificationType.PortalInvitation
                && notification.Status is NotificationStatus.Sent or NotificationStatus.Skipped or NotificationStatus.Abandoned)
            {
                notification.Body = NoticeTemplates.PortalInvitationScrubbedBody;
            }

            await unitOfWork.Notifications.UpdateAsync(notification);
            try
            {
                await unitOfWork.Commit(cancellationToken);
            }
            catch (DbUpdateConcurrencyException)
            {
                unitOfWork.ClearChanges();
                return false;
            }
            return delivered;
        }
    }
}
