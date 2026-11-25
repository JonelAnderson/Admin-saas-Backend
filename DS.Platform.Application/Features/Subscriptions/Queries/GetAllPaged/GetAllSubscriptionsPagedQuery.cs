using DS.Platform.Application.DTOs.Subscriptions.Response;
using DS.Platform.Application.Interfaces.Repositories;
using DS.Platform.Application.Interfaces.Repositories.Subscriptions;
using DS.Platform.Domain.Enums;
using DS.Platform.Shared.Commons.Bases.Request;
using DS.Platform.Shared.Localization;
using DS.Platform.Shared.Wrapper;
using MediatR;
using Microsoft.Extensions.Localization;

namespace DS.Platform.Application.Features.Subscriptions.Queries.GetAllPaged
{
    /// <summary>Listado de suscripciones (qué cliente tiene qué plan), filtrable por cliente, plan, producto y estado.</summary>
    public class GetAllSubscriptionsPagedQuery : IRequest<Result<IEnumerable<SubscriptionResponseDto>>>
    {
        public BaseFiltersRequest? BaseFiltersRequest { get; set; }
        public int? CustomerId { get; set; }
        public int? PlanId { get; set; }
        public PlanProduct? Product { get; set; }
        public SubscriptionStatus? Status { get; set; }
    }

    internal class GetAllSubscriptionsPagedQueryHandler(
        IUnitOfWork<int> unitOfWork,
        IStringLocalizer<ServerCommonResources> localizer) : IRequestHandler<GetAllSubscriptionsPagedQuery, Result<IEnumerable<SubscriptionResponseDto>>>
    {
        public async Task<Result<IEnumerable<SubscriptionResponseDto>>> Handle(GetAllSubscriptionsPagedQuery request, CancellationToken cancellationToken)
        {
            var (items, total) = await unitOfWork.Subscriptions.GetAllPaginatedAsync(
                request.BaseFiltersRequest ?? new BaseFiltersRequest(),
                new SubscriptionFilter(request.CustomerId, request.Product, request.Status, request.PlanId),
                cancellationToken);

            if (!items.Any())
            {
                return await Result<IEnumerable<SubscriptionResponseDto>>.SuccessAsync(
                    new List<SubscriptionResponseDto>(), localizer["MessageQueryEmpty"], total);
            }

            return await Result<IEnumerable<SubscriptionResponseDto>>.SuccessAsync(items, localizer["MessageQuery"], total);
        }
    }
}
