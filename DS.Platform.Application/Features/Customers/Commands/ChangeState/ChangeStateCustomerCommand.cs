using DS.Platform.Application.Interfaces.Repositories;
using DS.Platform.Shared.Localization;
using DS.Platform.Shared.Wrapper;
using MediatR;
using Microsoft.Extensions.Localization;

namespace DS.Platform.Application.Features.Customers.Commands.ChangeState
{
    /// <summary>
    /// Activa o desactiva un cliente: uno inactivo no aparece en el buscador ni recibe suscripciones nuevas. No suspende sus
    /// suscripciones (eso es una acción de la suscripción, con motivo).
    /// </summary>
    public class ChangeStateCustomerCommand : IRequest<Result<int>>
    {
        public int Id { get; set; }
    }

    internal class ChangeStateCustomerCommandHandler(
        IUnitOfWork<int> unitOfWork,
        IStringLocalizer<ServerCommonResources> localizer) : IRequestHandler<ChangeStateCustomerCommand, Result<int>>
    {
        public async Task<Result<int>> Handle(ChangeStateCustomerCommand request, CancellationToken cancellationToken)
        {
            var customer = await unitOfWork.Customers.GetByIdAsync(request.Id);
            if (customer is null || customer.IsDeleted)
            {
                return await Result<int>.FailAsync(localizer["MessageQueryEmpty"]);
            }

            customer.IsActive = !customer.IsActive;
            await unitOfWork.Customers.UpdateAsync(customer);
            await unitOfWork.Commit(cancellationToken);

            return await Result<int>.SuccessAsync(customer.Id, localizer["MessageUpdate"]);
        }
    }
}
