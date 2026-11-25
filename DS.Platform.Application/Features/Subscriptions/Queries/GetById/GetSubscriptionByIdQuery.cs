using DS.Platform.Application.DTOs.Subscriptions.Response;
using DS.Platform.Application.Interfaces.Repositories;
using DS.Platform.Shared.Localization;
using DS.Platform.Shared.Wrapper;
using MediatR;
using Microsoft.Extensions.Localization;

namespace DS.Platform.Application.Features.Subscriptions.Queries.GetById
{
    public class GetSubscriptionByIdQuery : IRequest<Result<SubscriptionResponseDto>>
    {
        public int Id { get; set; }
    }

    internal class GetSubscriptionByIdQueryHandler(
        IUnitOfWork<int> unitOfWork,
        IStringLocalizer<ServerCommonResources> localizer) : IRequestHandler<GetSubscriptionByIdQuery, Result<SubscriptionResponseDto>>
    {
        public async Task<Result<SubscriptionResponseDto>> Handle(GetSubscriptionByIdQuery request, CancellationToken cancellationToken)
        {
            var subscription = await unitOfWork.Subscriptions.GetResponseByIdAsync(request.Id, cancellationToken);
            if (subscription is null)
            {
                return await Result<SubscriptionResponseDto>.FailAsync(localizer["MessageQueryEmpty"]);
            }

            return await Result<SubscriptionResponseDto>.SuccessAsync(subscription);
        }
    }
}
