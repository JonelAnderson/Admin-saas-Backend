using DS.Platform.Application.DTOs.Payments.Response;
using DS.Platform.Application.Interfaces.Repositories;
using DS.Platform.Application.Interfaces.Repositories.Payments;
using DS.Platform.Domain.Enums;
using DS.Platform.Shared.Commons.Bases.Request;
using DS.Platform.Shared.Localization;
using DS.Platform.Shared.Wrapper;
using MediatR;
using Microsoft.Extensions.Localization;
using System.Globalization;

namespace DS.Platform.Application.Features.Payments.Queries.GetAllPaged
{
    /// <summary>
    /// Listado de pagos, del más reciente al más antiguo. Filtros: cliente, suscripción, estado, medio y fechas de pago
    /// (<c>StartDate</c> / <c>EndDate</c> de BaseFiltersRequest en yyyy-MM-dd, Lima, inclusivas).
    /// </summary>
    public class GetAllPaymentsPagedQuery : IRequest<Result<IEnumerable<PaymentResponseDto>>>
    {
        public BaseFiltersRequest? BaseFiltersRequest { get; set; }
        public int? CustomerId { get; set; }
        public int? SubscriptionId { get; set; }
        public PaymentStatus? Status { get; set; }
        public PaymentMethod? Method { get; set; }
    }

    internal class GetAllPaymentsPagedQueryHandler(
        IUnitOfWork<int> unitOfWork,
        IStringLocalizer<ServerCommonResources> localizer) : IRequestHandler<GetAllPaymentsPagedQuery, Result<IEnumerable<PaymentResponseDto>>>
    {
        public async Task<Result<IEnumerable<PaymentResponseDto>>> Handle(GetAllPaymentsPagedQuery request, CancellationToken cancellationToken)
        {
            var filters = request.BaseFiltersRequest ?? new BaseFiltersRequest();
            var filter = new PaymentFilter(request.CustomerId, request.SubscriptionId, request.Status, request.Method,
                ParseDate(filters.StartDate), ParseDate(filters.EndDate));

            var (items, total) = await unitOfWork.Payments.GetAllPaginatedAsync(filters, filter, cancellationToken);
            if (!items.Any())
            {
                return await Result<IEnumerable<PaymentResponseDto>>.SuccessAsync(
                    new List<PaymentResponseDto>(), localizer["MessageQueryEmpty"], total);
            }

            return await Result<IEnumerable<PaymentResponseDto>>.SuccessAsync(items, localizer["MessageQuery"], total);
        }

        private static DateOnly? ParseDate(string? value)
            => DateOnly.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date) ? date : null;
    }
}
