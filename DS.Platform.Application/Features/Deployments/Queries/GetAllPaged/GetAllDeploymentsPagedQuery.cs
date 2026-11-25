using DS.Platform.Application.DTOs.Deployments.Response;
using DS.Platform.Application.Interfaces.Repositories;
using DS.Platform.Application.Interfaces.Repositories.Ops;
using DS.Platform.Domain.Enums;
using DS.Platform.Shared.Commons.Bases.Request;
using DS.Platform.Shared.Localization;
using DS.Platform.Shared.Wrapper;
using MediatR;
using Microsoft.Extensions.Localization;

namespace DS.Platform.Application.Features.Deployments.Queries.GetAllPaged
{
    /// <summary>Bandeja de despliegues de instancias, filtrable por estado, instancia y cliente.</summary>
    public class GetAllDeploymentsPagedQuery : IRequest<Result<IEnumerable<InstanceDeploymentResponseDto>>>
    {
        public BaseFiltersRequest? BaseFiltersRequest { get; set; }
        public RemoteActionStatus? Status { get; set; }
        public int? InstanceId { get; set; }
        public int? CustomerId { get; set; }
    }

    internal class GetAllDeploymentsPagedQueryHandler(
        IUnitOfWork<int> unitOfWork,
        IStringLocalizer<ServerCommonResources> localizer) : IRequestHandler<GetAllDeploymentsPagedQuery, Result<IEnumerable<InstanceDeploymentResponseDto>>>
    {
        public async Task<Result<IEnumerable<InstanceDeploymentResponseDto>>> Handle(GetAllDeploymentsPagedQuery request, CancellationToken cancellationToken)
        {
            var (items, total) = await unitOfWork.InstanceDeployments.GetAllPaginatedAsync(
                request.BaseFiltersRequest ?? new BaseFiltersRequest(),
                new DeploymentFilter(request.Status, request.InstanceId, request.CustomerId), cancellationToken);

            return await Result<IEnumerable<InstanceDeploymentResponseDto>>.SuccessAsync(
                items, localizer[items.Any() ? "MessageQuery" : "MessageQueryEmpty"], total);
        }
    }
}
