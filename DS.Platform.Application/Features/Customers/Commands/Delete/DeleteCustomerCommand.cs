using DS.Platform.Application.Interfaces.Repositories;
using DS.Platform.Shared.Localization;
using DS.Platform.Shared.Wrapper;
using MediatR;
using Microsoft.Extensions.Localization;

namespace DS.Platform.Application.Features.Customers.Commands.Delete
{
    /// <summary>Borrado lógico de un cliente: solo si no tiene suscripciones abiertas (primero se cancelan).</summary>
    public class DeleteCustomerCommand : IRequest<Result<int>>
    {
        public int Id { get; set; }
    }

    internal class DeleteCustomerCommandHandler(
        IUnitOfWork<int> unitOfWork,
        IStringLocalizer<ServerCommonResources> localizer) : IRequestHandler<DeleteCustomerCommand, Result<int>>
    {
        public const string HasSubscriptionsMessage =
            "El cliente tiene suscripciones abiertas: cancélelas antes de eliminarlo (o desactívelo).";

        public const string HasInstancesMessage = "El cliente tiene instancias del ERP registradas: elimínelas antes de eliminarlo (o desactívelo).";

        public async Task<Result<int>> Handle(DeleteCustomerCommand request, CancellationToken cancellationToken)
        {
            var customer = await unitOfWork.Customers.GetByIdAsync(request.Id);
            if (customer is null || customer.IsDeleted)
            {
                return await Result<int>.FailAsync(localizer["MessageQueryEmpty"]);
            }

            if (await unitOfWork.Subscriptions.CustomerHasOpenAsync(customer.Id, cancellationToken))
            {
                return await Result<int>.FailAsync(HasSubscriptionsMessage);
            }

            if (await unitOfWork.ErpInstances.AnyForCustomerAsync(customer.Id, cancellationToken))
            {
                return await Result<int>.FailAsync(HasInstancesMessage);
            }

            await unitOfWork.Customers.DeleteAsync(customer);
            await unitOfWork.Commit(cancellationToken);

            return await Result<int>.SuccessAsync(customer.Id, localizer["MessageDelete"]);
        }
    }
}
