using DS.Platform.Application.DTOs.Customers.Request;
using DS.Platform.Application.Features.Customers.Commands.Create;
using DS.Platform.Application.Interfaces.Repositories;
using DS.Platform.Application.Mappings.Customers;
using DS.Platform.Shared.Localization;
using DS.Platform.Shared.Wrapper;
using MediatR;
using Microsoft.Extensions.Localization;

namespace DS.Platform.Application.Features.Customers.Commands.Update
{
    public class UpdateCustomerCommand : IRequest<Result<int>>
    {
        public UpdateCustomerRequestDto? UpdateCustomerRequestDto { get; set; }
    }

    internal class UpdateCustomerCommandHandler(
        IUnitOfWork<int> unitOfWork,
        IStringLocalizer<ServerCommonResources> localizer) : IRequestHandler<UpdateCustomerCommand, Result<int>>
    {
        public async Task<Result<int>> Handle(UpdateCustomerCommand command, CancellationToken cancellationToken)
        {
            var dto = command.UpdateCustomerRequestDto!;

            var customer = await unitOfWork.Customers.GetByIdAsync(dto.Id);
            if (customer is null || customer.IsDeleted)
            {
                return await Result<int>.FailAsync(localizer["MessageQueryEmpty"]);
            }

            if (!await unitOfWork.Customers.IsUniqueRucAsync(RucRules.Normalize(dto.Ruc), dto.Id, cancellationToken))
            {
                return await Result<int>.FailAsync(CreateCustomerCommandHandler.DuplicateRucMessage(dto.Ruc));
            }

            dto.ApplyTo(customer);
            await unitOfWork.Customers.UpdateAsync(customer);
            await unitOfWork.Commit(cancellationToken);

            return await Result<int>.SuccessAsync(customer.Id, localizer["MessageUpdate"]);
        }
    }
}
