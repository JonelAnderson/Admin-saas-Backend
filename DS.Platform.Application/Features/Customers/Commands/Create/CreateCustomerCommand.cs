using DS.Platform.Application.DTOs.Customers.Request;
using DS.Platform.Application.Interfaces.Repositories;
using DS.Platform.Application.Mappings.Customers;
using DS.Platform.Shared.Localization;
using DS.Platform.Shared.Wrapper;
using MediatR;
using Microsoft.Extensions.Localization;

namespace DS.Platform.Application.Features.Customers.Commands.Create
{
    public class CreateCustomerCommand : IRequest<Result<int>>
    {
        public CustomerRequestDto? CustomerRequestDto { get; set; }
    }

    internal class CreateCustomerCommandHandler(
        IUnitOfWork<int> unitOfWork,
        IStringLocalizer<ServerCommonResources> localizer) : IRequestHandler<CreateCustomerCommand, Result<int>>
    {
        public async Task<Result<int>> Handle(CreateCustomerCommand command, CancellationToken cancellationToken)
        {
            var dto = command.CustomerRequestDto!;

            if (!await unitOfWork.Customers.IsUniqueRucAsync(RucRules.Normalize(dto.Ruc), null, cancellationToken))
            {
                return await Result<int>.FailAsync(DuplicateRucMessage(dto.Ruc));
            }

            var customer = dto.ToEntity();
            await unitOfWork.Customers.AddAsync(customer);
            await unitOfWork.Commit(cancellationToken);

            return await Result<int>.SuccessAsync(customer.Id, localizer["MessageSave"]);
        }

        internal static string DuplicateRucMessage(string ruc) => $"Ya existe un cliente con el RUC {RucRules.Normalize(ruc)}.";
    }
}
