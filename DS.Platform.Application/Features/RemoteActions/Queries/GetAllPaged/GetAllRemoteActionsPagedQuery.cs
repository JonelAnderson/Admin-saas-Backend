using DS.Platform.Application.DTOs.RemoteActions.Response;
using DS.Platform.Application.Interfaces.Repositories;
using DS.Platform.Application.Interfaces.Repositories.Ops;
using DS.Platform.Domain.Enums;
using DS.Platform.Shared.Commons.Bases.Request;
using DS.Platform.Shared.Localization;
using DS.Platform.Shared.Wrapper;
using MediatR;
using Microsoft.Extensions.Localization;

namespace DS.Platform.Application.Features.RemoteActions.Queries.GetAllPaged
{
    /// <summary>Bandeja de acciones remotas, filtrable por estado, emisor y cliente.</summary>
    public class GetAllRemoteActionsPagedQuery : IRequest<Result<IEnumerable<RemoteActionResponseDto>>>
    {
        public BaseFiltersRequest? BaseFiltersRequest { get; set; }
        public RemoteActionStatus? Status { get; set; }
        public int? IssuerId { get; set; }
        public int? CustomerId { get; set; }
    }

    internal class GetAllRemoteActionsPagedQueryHandler(
        IUnitOfWork<int> unitOfWork,
        IStringLocalizer<ServerCommonResources> localizer) : IRequestHandler<GetAllRemoteActionsPagedQuery, Result<IEnumerable<RemoteActionResponseDto>>>
    {
        public async Task<Result<IEnumerable<RemoteActionResponseDto>>> Handle(GetAllRemoteActionsPagedQuery request, CancellationToken cancellationToken)
        {
            var (items, total) = await unitOfWork.RemoteActions.GetAllPaginatedAsync(
                request.BaseFiltersRequest ?? new BaseFiltersRequest(),
                new RemoteActionFilter(request.Status, request.IssuerId, request.CustomerId), cancellationToken);

            return await Result<IEnumerable<RemoteActionResponseDto>>.SuccessAsync(
                items, localizer[items.Any() ? "MessageQuery" : "MessageQueryEmpty"], total);
        }
    }
}
