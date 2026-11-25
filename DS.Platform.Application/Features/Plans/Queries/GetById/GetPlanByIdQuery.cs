using DS.Platform.Application.DTOs.Plans.Response;
using DS.Platform.Application.Interfaces.Repositories;
using DS.Platform.Application.Mappings.Plans;
using DS.Platform.Shared.Localization;
using DS.Platform.Shared.Wrapper;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;

namespace DS.Platform.Application.Features.Plans.Queries.GetById
{
    public class GetPlanByIdQuery : IRequest<Result<PlanResponseDto>>
    {
        public int Id { get; set; }
    }

    internal class GetPlanByIdQueryHandler(
        IUnitOfWork<int> unitOfWork,
        IStringLocalizer<ServerCommonResources> localizer) : IRequestHandler<GetPlanByIdQuery, Result<PlanResponseDto>>
    {
        public async Task<Result<PlanResponseDto>> Handle(GetPlanByIdQuery request, CancellationToken cancellationToken)
        {
            // GetAllQueryable ya filtra los borrados.
            var plan = await unitOfWork.Plans.GetAllQueryable().AsNoTracking()
                .Where(p => p.Id == request.Id)
                .Select(PlanMappings.ToResponse)
                .FirstOrDefaultAsync(cancellationToken);
            if (plan is null)
            {
                return await Result<PlanResponseDto>.FailAsync(localizer["MessageQueryEmpty"]);
            }

            return await Result<PlanResponseDto>.SuccessAsync(plan);
        }
    }
}
