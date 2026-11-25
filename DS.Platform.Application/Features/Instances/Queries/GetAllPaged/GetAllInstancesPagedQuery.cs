using DS.Platform.Application.DTOs.Instances.Response;
using DS.Platform.Application.Features.Instances.Common;
using DS.Platform.Application.Helpers;
using DS.Platform.Application.Interfaces.Repositories;
using DS.Platform.Application.Interfaces.Services;
using DS.Platform.Shared.Commons.Bases.Request;
using DS.Platform.Shared.Localization;
using DS.Platform.Shared.Wrapper;
using MediatR;
using Microsoft.Extensions.Localization;

namespace DS.Platform.Application.Features.Instances.Queries.GetAllPaged
{
    /// <summary>Instancias del ERP con su estado de licencia (el que recibirían hoy) y de contacto.</summary>
    public class GetAllInstancesPagedQuery : IRequest<Result<IEnumerable<InstanceResponseDto>>>
    {
        public BaseFiltersRequest? BaseFiltersRequest { get; set; }
        public int? CustomerId { get; set; }
    }

    internal class GetAllInstancesPagedQueryHandler(
        IUnitOfWork<int> unitOfWork,
        IDateTimeService dateTime,
        IStringLocalizer<ServerCommonResources> localizer) : IRequestHandler<GetAllInstancesPagedQuery, Result<IEnumerable<InstanceResponseDto>>>
    {
        public async Task<Result<IEnumerable<InstanceResponseDto>>> Handle(GetAllInstancesPagedQuery request, CancellationToken cancellationToken)
        {
            var (page, total) = await unitOfWork.ErpInstances.GetAllPaginatedAsync(
                request.BaseFiltersRequest ?? new BaseFiltersRequest(), request.CustomerId, cancellationToken);
            var items = page.ToList();

            // Suscripciones de todos los clientes de la página en una sola consulta (sin N+1).
            var now = dateTime.NowUtc;
            var today = LimaDate.Today(now);
            var subscriptions = await unitOfWork.Subscriptions.GetErpByCustomersAsync(
                items.Select(i => i.CustomerId).Distinct().ToList(), cancellationToken);
            var byCustomer = subscriptions.ToLookup(s => s.CustomerId);

            foreach (var item in items)
            {
                var state = LicenseStateBuilder.Build(byCustomer[item.CustomerId].ToList(), today);
                item.LicenseStatus = (int)state.Status;
                item.LicenseStatusName = state.Status switch
                {
                    Domain.Enums.LicenseStatus.Active => "Activa",
                    Domain.Enums.LicenseStatus.Grace => "En gracia",
                    Domain.Enums.LicenseStatus.Suspended => "Suspendida (solo lectura)",
                    _ => "Cancelada (solo lectura)"
                };
                (item.ContactName, item.ContactLevel) = InstanceContact.Describe(item.LastContactOnUtc, now);
            }

            return await Result<IEnumerable<InstanceResponseDto>>.SuccessAsync(
                items, localizer[items.Count > 0 ? "MessageQuery" : "MessageQueryEmpty"], total);
        }
    }
}
