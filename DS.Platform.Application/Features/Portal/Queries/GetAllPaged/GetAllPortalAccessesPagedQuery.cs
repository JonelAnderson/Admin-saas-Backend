using DS.Platform.Application.DTOs.Portal.Response;
using DS.Platform.Application.Interfaces.Repositories;
using DS.Platform.Application.Interfaces.Repositories.Ops;
using DS.Platform.Domain.Enums;
using DS.Platform.Shared.Commons.Bases.Request;
using DS.Platform.Shared.Localization;
using DS.Platform.Shared.Wrapper;
using MediatR;
using Microsoft.Extensions.Localization;

namespace DS.Platform.Application.Features.Portal.Queries.GetAllPaged
{
    /// <summary>Accesos al portal de clientes, filtrables por estado del trabajo, cliente y habilitado.</summary>
    public class GetAllPortalAccessesPagedQuery : IRequest<Result<IEnumerable<PortalAccessResponseDto>>>
    {
        public BaseFiltersRequest? BaseFiltersRequest { get; set; }
        public RemoteActionStatus? Status { get; set; }
        public int? CustomerId { get; set; }
        public bool? IsEnabled { get; set; }
    }

    internal class GetAllPortalAccessesPagedQueryHandler(
        IUnitOfWork<int> unitOfWork,
        IStringLocalizer<ServerCommonResources> localizer) : IRequestHandler<GetAllPortalAccessesPagedQuery, Result<IEnumerable<PortalAccessResponseDto>>>
    {
        public async Task<Result<IEnumerable<PortalAccessResponseDto>>> Handle(GetAllPortalAccessesPagedQuery request, CancellationToken cancellationToken)
        {
            var (items, total) = await unitOfWork.PortalAccesses.GetAllPaginatedAsync(
                request.BaseFiltersRequest ?? new BaseFiltersRequest(),
                new PortalAccessFilter(request.Status, request.CustomerId, request.IsEnabled), cancellationToken);

            return await Result<IEnumerable<PortalAccessResponseDto>>.SuccessAsync(
                items, localizer[items.Any() ? "MessageQuery" : "MessageQueryEmpty"], total);
        }
    }
}
