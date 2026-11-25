using DS.Platform.Application.DTOs.Customers.Response;
using DS.Platform.Application.Interfaces.Repositories;
using DS.Platform.Shared.Commons.Bases.Request;
using DS.Platform.Shared.Localization;
using DS.Platform.Shared.Wrapper;
using MediatR;
using Microsoft.Extensions.Localization;

namespace DS.Platform.Application.Features.Customers.Queries.GetAllPaged
{
    public class GetAllCustomersPagedQuery : IRequest<Result<IEnumerable<CustomerResponseDto>>>
    {
        public BaseFiltersRequest? BaseFiltersRequest { get; set; }
    }

    internal class GetAllCustomersPagedQueryHandler(
        IUnitOfWork<int> unitOfWork,
        IStringLocalizer<ServerCommonResources> localizer) : IRequestHandler<GetAllCustomersPagedQuery, Result<IEnumerable<CustomerResponseDto>>>
    {
        public async Task<Result<IEnumerable<CustomerResponseDto>>> Handle(GetAllCustomersPagedQuery request, CancellationToken cancellationToken)
        {
            var (items, total) = await unitOfWork.Customers.GetAllPaginatedAsync(
                request.BaseFiltersRequest ?? new BaseFiltersRequest(), cancellationToken);

            if (!items.Any())
            {
                return await Result<IEnumerable<CustomerResponseDto>>.SuccessAsync(
                    new List<CustomerResponseDto>(), localizer["MessageQueryEmpty"], total);
            }

            return await Result<IEnumerable<CustomerResponseDto>>.SuccessAsync(items, localizer["MessageQuery"], total);
        }
    }
}
