using DS.Platform.Application.DTOs.Customers.Response;
using DS.Platform.Application.Interfaces.Repositories;
using DS.Platform.Application.Mappings.Customers;
using DS.Platform.Shared.Localization;
using DS.Platform.Shared.Wrapper;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;

namespace DS.Platform.Application.Features.Customers.Queries.GetById
{
    public class GetCustomerByIdQuery : IRequest<Result<CustomerResponseDto>>
    {
        public int Id { get; set; }
    }

    internal class GetCustomerByIdQueryHandler(
        IUnitOfWork<int> unitOfWork,
        IStringLocalizer<ServerCommonResources> localizer) : IRequestHandler<GetCustomerByIdQuery, Result<CustomerResponseDto>>
    {
        public async Task<Result<CustomerResponseDto>> Handle(GetCustomerByIdQuery request, CancellationToken cancellationToken)
        {
            // GetAllQueryable ya filtra los borrados. Sus suscripciones: GET Subscription?customerId=…
            var customer = await unitOfWork.Customers.GetAllQueryable().AsNoTracking()
                .Where(c => c.Id == request.Id)
                .Select(CustomerMappings.ToResponse)
                .FirstOrDefaultAsync(cancellationToken);
            if (customer is null)
            {
                return await Result<CustomerResponseDto>.FailAsync(localizer["MessageQueryEmpty"]);
            }

            return await Result<CustomerResponseDto>.SuccessAsync(customer);
        }
    }
}
