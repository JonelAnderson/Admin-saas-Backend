using DS.Platform.Application.DTOs.Notifications.Response;
using DS.Platform.Application.Interfaces.Repositories;
using DS.Platform.Application.Interfaces.Repositories.Ops;
using DS.Platform.Domain.Enums;
using DS.Platform.Shared.Commons.Bases.Request;
using DS.Platform.Shared.Localization;
using DS.Platform.Shared.Wrapper;
using MediatR;
using Microsoft.Extensions.Localization;

namespace DS.Platform.Application.Features.Notifications.Queries.GetAllPaged
{
    /// <summary>Bandeja de avisos por correo, filtrable por estado, tipo y cliente.</summary>
    public class GetAllNotificationsPagedQuery : IRequest<Result<IEnumerable<NotificationResponseDto>>>
    {
        public BaseFiltersRequest? BaseFiltersRequest { get; set; }
        public NotificationStatus? Status { get; set; }
        public NotificationType? Type { get; set; }
        public int? CustomerId { get; set; }
    }

    internal class GetAllNotificationsPagedQueryHandler(
        IUnitOfWork<int> unitOfWork,
        IStringLocalizer<ServerCommonResources> localizer) : IRequestHandler<GetAllNotificationsPagedQuery, Result<IEnumerable<NotificationResponseDto>>>
    {
        public async Task<Result<IEnumerable<NotificationResponseDto>>> Handle(GetAllNotificationsPagedQuery request, CancellationToken cancellationToken)
        {
            var (items, total) = await unitOfWork.Notifications.GetAllPaginatedAsync(
                request.BaseFiltersRequest ?? new BaseFiltersRequest(),
                new NotificationFilter(request.Status, request.Type, request.CustomerId), cancellationToken);

            return await Result<IEnumerable<NotificationResponseDto>>.SuccessAsync(
                items, localizer[items.Any() ? "MessageQuery" : "MessageQueryEmpty"], total);
        }
    }
}
