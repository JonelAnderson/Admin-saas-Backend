using DS.Platform.Application.Interfaces.Repositories;
using DS.Platform.Domain.Enums;
using DS.Platform.Shared.Localization;
using DS.Platform.Shared.Wrapper;
using MediatR;
using Microsoft.Extensions.Localization;

namespace DS.Platform.Application.Features.ApiSunat.Commands.Unlink
{
    /// <summary>
    /// Desvincula una empresa de la API SUNAT del cliente (borrado lógico; la empresa sigue existiendo en la API). No se permite si
    /// la API la tiene suspendida por la consola: así no queda suspendida sin nadie que la reactive.
    /// </summary>
    public class UnlinkIssuerCommand : IRequest<Result<int>>
    {
        public int Id { get; set; }
    }

    internal class UnlinkIssuerCommandHandler(
        IUnitOfWork<int> unitOfWork,
        IStringLocalizer<ServerCommonResources> localizer) : IRequestHandler<UnlinkIssuerCommand, Result<int>>
    {
        public const string SuspendedMessage =
            "La emisión de esta empresa está suspendida por la consola: regularice la suscripción del cliente (o reactívela) antes de desvincularla.";

        public async Task<Result<int>> Handle(UnlinkIssuerCommand request, CancellationToken cancellationToken)
        {
            var issuer = await unitOfWork.ApiSunatIssuers.GetByIdAsync(request.Id);
            if (issuer is null || issuer.IsDeleted)
            {
                return await Result<int>.FailAsync(localizer["MessageQueryEmpty"]);
            }
            if (issuer.SuspendApplied || issuer.SuspendDesired)
            {
                return await Result<int>.FailAsync(SuspendedMessage);
            }

            // Lo que quedara en la bandeja ya no tiene a quién aplicarse.
            foreach (var open in await unitOfWork.RemoteActions.GetOpenForIssuerAsync(issuer.Id, cancellationToken))
            {
                open.Status = RemoteActionStatus.Cancelled;
                open.LockedUntilUtc = null;
                open.LastError = "La empresa se desvinculó del cliente.";
                await unitOfWork.RemoteActions.UpdateAsync(open);
            }

            await unitOfWork.ApiSunatIssuers.DeleteAsync(issuer);
            await unitOfWork.Commit(cancellationToken);

            return await Result<int>.SuccessAsync(issuer.Id, localizer["MessageDelete"]);
        }
    }
}
