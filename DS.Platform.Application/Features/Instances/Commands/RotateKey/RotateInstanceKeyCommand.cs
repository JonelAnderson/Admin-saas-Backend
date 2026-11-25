using DS.Platform.Application.DTOs.Instances.Response;
using DS.Platform.Application.Features.Instances.Common;
using DS.Platform.Application.Interfaces.Repositories;
using DS.Platform.Application.Interfaces.Services;
using DS.Platform.Shared.Localization;
using DS.Platform.Shared.Wrapper;
using MediatR;
using Microsoft.Extensions.Localization;

namespace DS.Platform.Application.Features.Instances.Commands.RotateKey
{
    /// <summary>
    /// Genera una clave de licencia nueva para la instancia (la anterior deja de valer al instante: la instancia queda sin contacto
    /// hasta que se actualice su <c>License__Key</c>). La clave nueva viaja en la respuesta una sola vez.
    /// </summary>
    public class RotateInstanceKeyCommand : IRequest<Result<InstanceKeyResponseDto>>
    {
        public int Id { get; set; }
    }

    internal class RotateInstanceKeyCommandHandler(
        IUnitOfWork<int> unitOfWork,
        IDateTimeService dateTime,
        IStringLocalizer<ServerCommonResources> localizer) : IRequestHandler<RotateInstanceKeyCommand, Result<InstanceKeyResponseDto>>
    {
        public async Task<Result<InstanceKeyResponseDto>> Handle(RotateInstanceKeyCommand request, CancellationToken cancellationToken)
        {
            var instance = await unitOfWork.ErpInstances.GetByIdAsync(request.Id);
            if (instance is null || instance.IsDeleted)
            {
                return await Result<InstanceKeyResponseDto>.FailAsync(localizer["MessageQueryEmpty"]);
            }

            var key = LicenseKeys.Generate();
            instance.LicenseKeyHash = LicenseKeys.Hash(key);
            instance.LicenseKeyHint = LicenseKeys.Hint(key);
            instance.KeyRotatedOnUtc = dateTime.NowUtc;
            await unitOfWork.ErpInstances.UpdateAsync(instance);
            await unitOfWork.Commit(cancellationToken);

            return await Result<InstanceKeyResponseDto>.SuccessAsync(
                new InstanceKeyResponseDto { Id = instance.Id, LicenseKey = key }, localizer["MessageUpdate"]);
        }
    }
}
