using DS.Platform.Application.DTOs.Customers.Response;
using DS.Platform.Application.Interfaces.Repositories;
using DS.Platform.Shared.Wrapper;
using MediatR;

namespace DS.Platform.Application.Features.Customers.Queries.Lookup
{
    /// <summary>Buscador de clientes activos por RUC o nombre para los combos (como los Lookup del ERP: máx. 30 filas).</summary>
    public class LookupCustomersQuery : IRequest<Result<IEnumerable<CustomerLookupResponseDto>>>
    {
        public const int MaxRows = 30;
        public string? Text { get; set; }
    }

    internal class LookupCustomersQueryHandler(IUnitOfWork<int> unitOfWork)
        : IRequestHandler<LookupCustomersQuery, Result<IEnumerable<CustomerLookupResponseDto>>>
    {
        public async Task<Result<IEnumerable<CustomerLookupResponseDto>>> Handle(LookupCustomersQuery request, CancellationToken cancellationToken)
        {
            var items = await unitOfWork.Customers.LookupAsync(request.Text, LookupCustomersQuery.MaxRows, cancellationToken);
            return await Result<IEnumerable<CustomerLookupResponseDto>>.SuccessAsync(items);
        }
    }
}
