using DS.Platform.Application.DTOs.Instances.Request;
using DS.Platform.Application.Features.Instances.Commands.Create;
using DS.Platform.Application.Interfaces.Repositories;
using DS.Platform.Application.Mappings.Instances;
using DS.Platform.Shared.Localization;
using DS.Platform.Shared.Wrapper;
using MediatR;
using Microsoft.Extensions.Localization;

namespace DS.Platform.Application.Features.Instances.Commands.Update
{
    /// <summary>Edita nombre, dirección, servidor y notas de la instancia (no el cliente, la clave ni lo que ella informa).</summary>
    public class UpdateInstanceCommand : IRequest<Result<int>>
    {
        public UpdateInstanceRequestDto? UpdateInstanceRequestDto { get; set; }
    }

    internal class UpdateInstanceCommandHandler(
        IUnitOfWork<int> unitOfWork,
        IStringLocalizer<ServerCommonResources> localizer) : IRequestHandler<UpdateInstanceCommand, Result<int>>
    {
        public async Task<Result<int>> Handle(UpdateInstanceCommand command, CancellationToken cancellationToken)
        {
            var dto = command.UpdateInstanceRequestDto!;

            var instance = await unitOfWork.ErpInstances.GetByIdAsync(dto.Id);
            if (instance is null || instance.IsDeleted)
            {
                return await Result<int>.FailAsync(localizer["MessageQueryEmpty"]);
            }
            if (await unitOfWork.ErpInstances.NameExistsAsync(instance.CustomerId, dto.Name, instance.Id, cancellationToken))
            {
                return await Result<int>.FailAsync(CreateInstanceCommandHandler.DuplicateNameMessage);
            }

            dto.ApplyTo(instance);
            await unitOfWork.ErpInstances.UpdateAsync(instance);
            await unitOfWork.Commit(cancellationToken);

            return await Result<int>.SuccessAsync(instance.Id, localizer["MessageUpdate"]);
        }
    }
}
