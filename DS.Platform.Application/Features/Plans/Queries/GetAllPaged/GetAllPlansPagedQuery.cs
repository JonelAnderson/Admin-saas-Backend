using DS.Platform.Application.DTOs.Plans.Response;
using DS.Platform.Application.Interfaces.Repositories;
using DS.Platform.Domain.Enums;
using DS.Platform.Shared.Commons.Bases.Request;
using DS.Platform.Shared.Localization;
using DS.Platform.Shared.Wrapper;
using MediatR;
using Microsoft.Extensions.Localization;

namespace DS.Platform.Application.Features.Plans.Queries.GetAllPaged
{
    public class GetAllPlansPagedQuery : IRequest<Result<IEnumerable<PlanResponseDto>>>
    {
        public BaseFiltersRequest? BaseFiltersRequest { get; set; }
        /// <summary>Solo los planes de un producto (null = todos).</summary>
        public PlanProduct? Product { get; set; }
    }

    internal class GetAllPlansPagedQueryHandler(
        IUnitOfWork<int> unitOfWork,
        IStringLocalizer<ServerCommonResources> localizer) : IRequestHandler<GetAllPlansPagedQuery, Result<IEnumerable<PlanResponseDto>>>
    {
        public async Task<Result<IEnumerable<PlanResponseDto>>> Handle(GetAllPlansPagedQuery request, CancellationToken cancellationToken)
        {
            var (items, total) = await unitOfWork.Plans.GetAllPaginatedAsync(
                request.BaseFiltersRequest ?? new BaseFiltersRequest(), request.Product, cancellationToken);

            if (!items.Any())
            {
                return await Result<IEnumerable<PlanResponseDto>>.SuccessAsync(
                    new List<PlanResponseDto>(), localizer["MessageQueryEmpty"], total);
            }

            return await Result<IEnumerable<PlanResponseDto>>.SuccessAsync(items, localizer["MessageQuery"], total);
        }
    }
}
