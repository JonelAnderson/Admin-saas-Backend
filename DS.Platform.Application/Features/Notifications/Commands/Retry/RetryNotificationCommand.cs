using DS.Platform.Application.Interfaces.Repositories;
using DS.Platform.Application.Interfaces.Services;
using DS.Platform.Domain.Enums;
using DS.Platform.Shared.Localization;
using DS.Platform.Shared.Wrapper;
using MediatR;
using Microsoft.Extensions.Localization;

namespace DS.Platform.Application.Features.Notifications.Commands.Retry
{
    /// <summary>
    /// Reintenta a mano un aviso Fallido o Abandonado: vuelve a Pendiente con los intentos en cero (y la caducidad de 3 días vuelve a
    /// contar desde ahora) para que el siguiente pase del despachador lo envíe. No envía el correo.
    /// </summary>
    public class RetryNotificationCommand : IRequest<Result<int>>
    {
        public int Id { get; set; }
    }

    internal class RetryNotificationCommandHandler(
        IUnitOfWork<int> unitOfWork,
        IDateTimeService dateTime,
        IStringLocalizer<ServerCommonResources> localizer) : IRequestHandler<RetryNotificationCommand, Result<int>>
    {
        public const string NotRetryableMessage = "Solo se reintentan los avisos fallidos o abandonados.";
        public const string PortalInvitationMessage = "La invitación al portal ya no conserva su enlace: reinvite a la persona desde la pantalla Portal.";

        public async Task<Result<int>> Handle(RetryNotificationCommand request, CancellationToken cancellationToken)
        {
            var notification = await unitOfWork.Notifications.GetByIdAsync(request.Id);
            if (notification is null || notification.IsDeleted)
            {
                return await Result<int>.FailAsync(localizer["MessageQueryEmpty"]);
            }
            if (notification.Status is not (NotificationStatus.Failed or NotificationStatus.Abandoned))
            {
                return await Result<int>.FailAsync(NotRetryableMessage);
            }

            if (notification.Type == NotificationType.PortalInvitation && notification.Status == NotificationStatus.Abandoned)
            {
                return await Result<int>.FailAsync(PortalInvitationMessage);
            }

            notification.Status = NotificationStatus.Pending;
            notification.Attempts = 0;
            notification.NextAttemptOnUtc = dateTime.NowUtc;
            notification.QueuedOnUtc = dateTime.NowUtc;
            notification.LockedUntilUtc = null;
            await unitOfWork.Notifications.UpdateAsync(notification);
            await unitOfWork.Commit(cancellationToken);

            return await Result<int>.SuccessAsync(notification.Id, localizer["MessageUpdate"]);
        }
    }
}
