using DS.Platform.Application.Interfaces.Repositories;
using DS.Platform.Application.Interfaces.Services;
using DS.Platform.Domain.Enums;
using DS.Platform.Shared.Wrapper;
using MediatR;

namespace DS.Platform.Application.Features.Portal.Commands.Retry
{
    /// <summary>Devuelve a Pendiente un acceso fallido o abandonado (el despachador lo vuelve a intentar desde cero).</summary>
    public class RetryPortalAccessCommand : IRequest<Result<int>>
    {
        public int Id { get; set; }
    }

    internal class RetryPortalAccessCommandHandler(
        IUnitOfWork<int> unitOfWork,
        IDateTimeService dateTime) : IRequestHandler<RetryPortalAccessCommand, Result<int>>
    {
        public const string NotFoundMessage = "El acceso al portal no existe.";
        public const string NotRetryableMessage = "Solo se reintentan los accesos fallidos o abandonados.";
        public const string DoneMessage = "Reintento programado.";

        public async Task<Result<int>> Handle(RetryPortalAccessCommand command, CancellationToken cancellationToken)
        {
            var access = await unitOfWork.PortalAccesses.GetByIdAsync(command.Id);
            if (access is null || access.IsDeleted)
            {
                return await Result<int>.FailAsync(NotFoundMessage);
            }
            if (access.Status is not (RemoteActionStatus.Failed or RemoteActionStatus.Abandoned))
            {
                return await Result<int>.FailAsync(NotRetryableMessage);
            }

            access.Status = RemoteActionStatus.Pending;
            access.Attempts = 0;
            access.NextAttemptOnUtc = dateTime.NowUtc;
            access.LockedUntilUtc = null;
            await unitOfWork.PortalAccesses.UpdateAsync(access);
            await unitOfWork.Commit(cancellationToken);
            return await Result<int>.SuccessAsync(access.Id, DoneMessage);
        }
    }
}
