using DS.Platform.Application.DTOs.ApiSunat.Response;
using DS.Platform.Application.Interfaces.Repositories;
using DS.Platform.Application.Interfaces.Repositories.ApiSunat;
using DS.Platform.Shared.Commons.Bases.Request;
using DS.Platform.Shared.Localization;
using DS.Platform.Shared.Wrapper;
using MediatR;
using Microsoft.Extensions.Localization;

namespace DS.Platform.Application.Features.ApiSunat.Queries.GetAllPaged
{
    /// <summary>Empresas emisoras de la API SUNAT vinculadas a clientes, filtrables por cliente y por suspensión.</summary>
    public class GetAllIssuersPagedQuery : IRequest<Result<IEnumerable<IssuerResponseDto>>>
    {
        public BaseFiltersRequest? BaseFiltersRequest { get; set; }
        public int? CustomerId { get; set; }
        public bool? Suspended { get; set; }
    }

    internal class GetAllIssuersPagedQueryHandler(
        IUnitOfWork<int> unitOfWork,
        IStringLocalizer<ServerCommonResources> localizer) : IRequestHandler<GetAllIssuersPagedQuery, Result<IEnumerable<IssuerResponseDto>>>
    {
        public async Task<Result<IEnumerable<IssuerResponseDto>>> Handle(GetAllIssuersPagedQuery request, CancellationToken cancellationToken)
        {
            var (items, total) = await unitOfWork.ApiSunatIssuers.GetAllPaginatedAsync(
                request.BaseFiltersRequest ?? new BaseFiltersRequest(), new IssuerFilter(request.CustomerId, request.Suspended), cancellationToken);

            return await Result<IEnumerable<IssuerResponseDto>>.SuccessAsync(
                items, localizer[items.Any() ? "MessageQuery" : "MessageQueryEmpty"], total);
        }
    }
}
