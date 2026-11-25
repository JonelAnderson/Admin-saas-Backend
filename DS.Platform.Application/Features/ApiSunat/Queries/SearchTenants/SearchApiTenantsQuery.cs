using DS.Platform.Application.DTOs.ApiSunat.Response;
using DS.Platform.Application.Features.ApiSunat.Commands.Link;
using DS.Platform.Application.Interfaces.Repositories;
using DS.Platform.Application.Interfaces.Services.ApiSunat;
using DS.Platform.Application.Mappings.ApiSunat;
using DS.Platform.Shared.Localization;
using DS.Platform.Shared.Wrapper;
using MediatR;
using Microsoft.Extensions.Localization;

namespace DS.Platform.Application.Features.ApiSunat.Queries.SearchTenants
{
    /// <summary>
    /// Busca empresas en la API SUNAT (por RUC o razón social) para vincularlas a un cliente, marcando las que ya están vinculadas.
    /// Máximo 30 por búsqueda, como los demás buscadores de la consola. Solo lee: nunca llama a la API para cambiar nada.
    /// </summary>
    public class SearchApiTenantsQuery : IRequest<Result<IEnumerable<ApiTenantResponseDto>>>
    {
        public string? Text { get; set; }
    }

    internal class SearchApiTenantsQueryHandler(
        IUnitOfWork<int> unitOfWork,
        IApiSunatAdminClient apiSunat,
        IStringLocalizer<ServerCommonResources> localizer) : IRequestHandler<SearchApiTenantsQuery, Result<IEnumerable<ApiTenantResponseDto>>>
    {
        public const int MaxRows = 30;

        public async Task<Result<IEnumerable<ApiTenantResponseDto>>> Handle(SearchApiTenantsQuery request, CancellationToken cancellationToken)
        {
            var text = string.IsNullOrWhiteSpace(request.Text) ? null : request.Text.Trim();
            var call = await apiSunat.SearchTenantsAsync(text, 1, MaxRows, cancellationToken);
            if (!call.Success)
            {
                return await Result<IEnumerable<ApiTenantResponseDto>>.FailAsync(LinkIssuerCommandHandler.ApiUnavailableMessage(call.Error));
            }

            var tenants = call.Data!.Items.Take(MaxRows).ToList();
            var linked = await unitOfWork.ApiSunatIssuers.GetLinkedTenantIdsAsync(tenants.Select(t => t.Id).ToList(), cancellationToken);
            var items = tenants.Select(t => t.ToDto(linked.Contains(t.Id))).ToList();

            return await Result<IEnumerable<ApiTenantResponseDto>>.SuccessAsync(
                items, localizer[items.Count > 0 ? "MessageQuery" : "MessageQueryEmpty"], (int)Math.Min(call.Data.Total, int.MaxValue));
        }
    }
}
