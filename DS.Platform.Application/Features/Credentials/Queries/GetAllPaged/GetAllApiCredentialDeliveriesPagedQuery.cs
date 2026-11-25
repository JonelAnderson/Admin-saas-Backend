using DS.Platform.Application.DTOs.Credentials.Response;
using DS.Platform.Application.Interfaces.Repositories;
using DS.Platform.Application.Interfaces.Repositories.Ops;
using DS.Platform.Domain.Enums;
using DS.Platform.Shared.Commons.Bases.Request;
using DS.Platform.Shared.Localization;
using DS.Platform.Shared.Wrapper;
using MediatR;
using Microsoft.Extensions.Localization;

namespace DS.Platform.Application.Features.Credentials.Queries.GetAllPaged
{
    /// <summary>Entregas de credenciales al ERP, filtrables por estado, cliente e instancia. Nunca trae la API key ni el secret.</summary>
    public class GetAllApiCredentialDeliveriesPagedQuery : IRequest<Result<IEnumerable<ApiCredentialDeliveryResponseDto>>>
    {
        public BaseFiltersRequest? BaseFiltersRequest { get; set; }
        public ApiCredentialDeliveryStatus? Status { get; set; }
        public int? CustomerId { get; set; }
        public int? InstanceId { get; set; }
    }

    internal class GetAllApiCredentialDeliveriesPagedQueryHandler(
        IUnitOfWork<int> unitOfWork,
        IStringLocalizer<ServerCommonResources> localizer) : IRequestHandler<GetAllApiCredentialDeliveriesPagedQuery, Result<IEnumerable<ApiCredentialDeliveryResponseDto>>>
    {
        public async Task<Result<IEnumerable<ApiCredentialDeliveryResponseDto>>> Handle(GetAllApiCredentialDeliveriesPagedQuery request, CancellationToken cancellationToken)
        {
            var (items, total) = await unitOfWork.ApiCredentialDeliveries.GetAllPaginatedAsync(
                request.BaseFiltersRequest ?? new BaseFiltersRequest(),
                new ApiCredentialDeliveryFilter(request.Status, request.CustomerId, request.InstanceId), cancellationToken);

            return await Result<IEnumerable<ApiCredentialDeliveryResponseDto>>.SuccessAsync(
                items, localizer[items.Any() ? "MessageQuery" : "MessageQueryEmpty"], total);
        }
    }
}
