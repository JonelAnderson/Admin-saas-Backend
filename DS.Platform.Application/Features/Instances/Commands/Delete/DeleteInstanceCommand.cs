using DS.Platform.Application.Interfaces.Repositories;
using DS.Platform.Shared.Localization;
using DS.Platform.Shared.Wrapper;
using MediatR;
using Microsoft.Extensions.Localization;

namespace DS.Platform.Application.Features.Instances.Commands.Delete
{
    /// <summary>Borrado lógico de una instancia: su clave deja de valer y ya no se le entrega licencia.</summary>
    public class DeleteInstanceCommand : IRequest<Result<int>>
    {
        public int Id { get; set; }
    }

    internal class DeleteInstanceCommandHandler(
        IUnitOfWork<int> unitOfWork,
        IStringLocalizer<ServerCommonResources> localizer) : IRequestHandler<DeleteInstanceCommand, Result<int>>
    {
        public const string HasStackMessage = "La instancia tiene su stack en Coolify: elimínelo allí primero (la consola no borra la base del cliente) y luego desvincule la instancia.";
        public const string OpenDeploymentMessage = "La instancia tiene un despliegue sin terminar: espere a que termine o cancélelo.";

        public async Task<Result<int>> Handle(DeleteInstanceCommand request, CancellationToken cancellationToken)
        {
            var instance = await unitOfWork.ErpInstances.GetByIdAsync(request.Id);
            if (instance is null || instance.IsDeleted)
            {
                return await Result<int>.FailAsync(localizer["MessageQueryEmpty"]);
            }

            if (!string.IsNullOrEmpty(instance.CoolifyServiceUuid))
            {
                return await Result<int>.FailAsync(HasStackMessage);
            }
            if (await unitOfWork.InstanceDeployments.GetOpenForInstanceAsync(instance.Id, cancellationToken) is not null)
            {
                return await Result<int>.FailAsync(OpenDeploymentMessage);
            }

            await unitOfWork.ErpInstances.DeleteAsync(instance);
            await unitOfWork.Commit(cancellationToken);

            return await Result<int>.SuccessAsync(instance.Id, localizer["MessageDelete"]);
        }
    }
}
