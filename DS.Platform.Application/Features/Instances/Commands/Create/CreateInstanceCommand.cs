using DS.Platform.Application.DTOs.Instances.Request;
using DS.Platform.Application.DTOs.Instances.Response;
using DS.Platform.Application.Features.Instances.Common;
using DS.Platform.Application.Interfaces.Repositories;
using DS.Platform.Application.Interfaces.Services;
using DS.Platform.Application.Mappings.Instances;
using DS.Platform.Shared.Localization;
using DS.Platform.Shared.Wrapper;
using MediatR;
using Microsoft.Extensions.Localization;

namespace DS.Platform.Application.Features.Instances.Commands.Create
{
    /// <summary>
    /// Registra una instancia del ERP de un cliente y genera su clave de licencia. La clave viaja en la respuesta una sola vez:
    /// en la base solo queda su hash. Se pone en <c>License__Key</c> de la instancia.
    /// </summary>
    public class CreateInstanceCommand : IRequest<Result<InstanceKeyResponseDto>>
    {
        public CreateInstanceRequestDto? CreateInstanceRequestDto { get; set; }
    }

    internal class CreateInstanceCommandHandler(
        IUnitOfWork<int> unitOfWork,
        IDateTimeService dateTime,
        IStringLocalizer<ServerCommonResources> localizer) : IRequestHandler<CreateInstanceCommand, Result<InstanceKeyResponseDto>>
    {
        public const string CustomerNotFoundMessage = "El cliente no existe.";
        public const string CustomerInactiveMessage = "El cliente está inactivo: actívelo antes de registrarle una instancia.";
        public const string DuplicateNameMessage = "El cliente ya tiene una instancia con ese nombre.";

        public async Task<Result<InstanceKeyResponseDto>> Handle(CreateInstanceCommand command, CancellationToken cancellationToken)
        {
            var dto = command.CreateInstanceRequestDto!;

            var customer = await unitOfWork.Customers.GetByIdAsync(dto.CustomerId);
            if (customer is null || customer.IsDeleted)
            {
                return await Result<InstanceKeyResponseDto>.FailAsync(CustomerNotFoundMessage);
            }
            if (!customer.IsActive)
            {
                return await Result<InstanceKeyResponseDto>.FailAsync(CustomerInactiveMessage);
            }
            if (await unitOfWork.ErpInstances.NameExistsAsync(customer.Id, dto.Name, null, cancellationToken))
            {
                return await Result<InstanceKeyResponseDto>.FailAsync(DuplicateNameMessage);
            }

            var key = LicenseKeys.Generate();
            var instance = dto.ToEntity(LicenseKeys.Hash(key), LicenseKeys.Hint(key), dateTime.NowUtc);
            await unitOfWork.ErpInstances.AddAsync(instance);
            await unitOfWork.Commit(cancellationToken);

            return await Result<InstanceKeyResponseDto>.SuccessAsync(
                new InstanceKeyResponseDto { Id = instance.Id, LicenseKey = key }, localizer["MessageSave"]);
        }
    }
}
