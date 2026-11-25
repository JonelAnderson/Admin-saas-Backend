using DS.Platform.Application.DTOs.Plans.Response;
using DS.Platform.Application.Interfaces.Repositories;
using DS.Platform.Domain.Enums;
using DS.Platform.Shared.Wrapper;
using MediatR;

namespace DS.Platform.Application.Features.Plans.Queries.GetSelect
{
    /// <summary>Planes activos para los combos (p. ej. al crear una suscripción).</summary>
    public class GetPlanSelectQuery : IRequest<Result<IEnumerable<PlanSelectResponseDto>>>
    {
        public PlanProduct? Product { get; set; }
    }

    internal class GetPlanSelectQueryHandler(IUnitOfWork<int> unitOfWork)
        : IRequestHandler<GetPlanSelectQuery, Result<IEnumerable<PlanSelectResponseDto>>>
    {
        public async Task<Result<IEnumerable<PlanSelectResponseDto>>> Handle(GetPlanSelectQuery request, CancellationToken cancellationToken)
        {
            var items = await unitOfWork.Plans.GetSelectAsync(request.Product, cancellationToken);
            return await Result<IEnumerable<PlanSelectResponseDto>>.SuccessAsync(items);
        }
    }
}
