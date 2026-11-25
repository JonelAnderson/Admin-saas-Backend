using DS.Platform.Application.Interfaces.Repositories;
using DS.Platform.Application.Interfaces.Services;
using DS.Platform.Domain.Enums;
using DS.Platform.Shared.Localization;
using DS.Platform.Shared.Wrapper;
using MediatR;
using Microsoft.Extensions.Localization;

namespace DS.Platform.Application.Features.RemoteActions.Commands.Retry
{
    /// <summary>
    /// Reintenta a mano una acción Fallida o Abandonada: vuelve a Pendiente con los intentos en cero para que el siguiente pase del
    /// despachador la envíe. No llama a la API (las llamadas externas solo salen por la bandeja).
    /// </summary>
    public class RetryRemoteActionCommand : IRequest<Result<int>>
    {
        public int Id { get; set; }
    }

    internal class RetryRemoteActionCommandHandler(
        IUnitOfWork<int> unitOfWork,
        IDateTimeService dateTime,
        IStringLocalizer<ServerCommonResources> localizer) : IRequestHandler<RetryRemoteActionCommand, Result<int>>
    {
        public const string NotRetryableMessage = "Solo se reintentan las acciones fallidas o abandonadas.";

        public async Task<Result<int>> Handle(RetryRemoteActionCommand request, CancellationToken cancellationToken)
        {
            var action = await unitOfWork.RemoteActions.GetByIdAsync(request.Id);
            if (action is null || action.IsDeleted)
            {
                return await Result<int>.FailAsync(localizer["MessageQueryEmpty"]);
            }
            if (action.Status is not (RemoteActionStatus.Failed or RemoteActionStatus.Abandoned))
            {
                return await Result<int>.FailAsync(NotRetryableMessage);
            }

            action.Status = RemoteActionStatus.Pending;
            action.Attempts = 0;
            action.NextAttemptOnUtc = dateTime.NowUtc;
            action.LockedUntilUtc = null;
            await unitOfWork.RemoteActions.UpdateAsync(action);
            await unitOfWork.Commit(cancellationToken);

            return await Result<int>.SuccessAsync(action.Id, localizer["MessageUpdate"]);
        }
    }
}
