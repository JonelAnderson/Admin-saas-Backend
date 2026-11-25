using DS.Platform.Application.Interfaces.Repositories;
using DS.Platform.Application.Interfaces.Services;
using DS.Platform.Domain.Enums;
using DS.Platform.Shared.Wrapper;
using MediatR;

namespace DS.Platform.Application.Features.Portal.Commands.SetEnabled
{
    /// <summary>
    /// Deshabilita o vuelve a habilitar a una persona del portal. Solo guarda lo que la consola quiere (<c>IsEnabled</c>) y el trabajo
    /// pendiente: el despachador se lo cuenta a la API (deshabilitar cierra sus sesiones allí). Quien nunca llegó a ser invitado en la
    /// API (sin usuario) no tiene a quién habilitar: se deshabilita cancelando su invitación y se habilita invitándolo de nuevo.
    /// </summary>
    public class SetPortalAccessEnabledCommand : IRequest<Result<int>>
    {
        public int Id { get; set; }
        public bool Enabled { get; set; }
    }

    internal class SetPortalAccessEnabledCommandHandler(
        IUnitOfWork<int> unitOfWork,
        IDateTimeService dateTime) : IRequestHandler<SetPortalAccessEnabledCommand, Result<int>>
    {
        public const string NotFoundMessage = "El acceso al portal no existe.";
        public const string BusyMessage = "El acceso se está procesando en este momento: inténtelo en un minuto.";
        public const string NeverInvitedMessage = "Esa persona aún no tiene usuario en el portal: invítela de nuevo en lugar de habilitarla.";
        public const string DisabledMessage = "Acceso deshabilitado: se cerrarán sus sesiones del portal en unos minutos.";
        public const string EnabledMessage = "Acceso habilitado: la persona vuelve a entrar con su contraseña.";
        public const string CancelledMessage = "Invitación cancelada: la persona no tenía usuario todavía.";

        public async Task<Result<int>> Handle(SetPortalAccessEnabledCommand command, CancellationToken cancellationToken)
        {
            var access = await unitOfWork.PortalAccesses.GetByIdAsync(command.Id);
            if (access is null || access.IsDeleted)
            {
                return await Result<int>.FailAsync(NotFoundMessage);
            }
            var now = dateTime.NowUtc;
            if (access.Status == RemoteActionStatus.Sending && access.LockedUntilUtc > now)
            {
                return await Result<int>.FailAsync(BusyMessage);
            }

            if (access.ApiUserId is null)
            {
                if (command.Enabled)
                {
                    return await Result<int>.FailAsync(NeverInvitedMessage);
                }
                access.IsEnabled = false;
                access.Status = RemoteActionStatus.Cancelled;
                access.LockedUntilUtc = null;
                await unitOfWork.PortalAccesses.UpdateAsync(access);
                await unitOfWork.Commit(cancellationToken);
                return await Result<int>.SuccessAsync(access.Id, CancelledMessage);
            }

            access.IsEnabled = command.Enabled;
            access.WorkType = PortalWorkType.SetState;
            access.Status = RemoteActionStatus.Pending;
            access.Attempts = 0;
            access.NextAttemptOnUtc = now;
            access.LockedUntilUtc = null;
            access.LastError = null;
            access.DoneOnUtc = null;
            await unitOfWork.PortalAccesses.UpdateAsync(access);
            await unitOfWork.Commit(cancellationToken);
            return await Result<int>.SuccessAsync(access.Id, command.Enabled ? EnabledMessage : DisabledMessage);
        }
    }
}
