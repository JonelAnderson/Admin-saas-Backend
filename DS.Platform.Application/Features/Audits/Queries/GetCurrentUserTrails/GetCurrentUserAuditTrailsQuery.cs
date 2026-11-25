using System.Linq;
using DS.Platform.Application.Mappings.Audits;
using DS.Platform.Application.DTOs.Audits.Response;
using DS.Platform.Application.Interfaces.Repositories;
using DS.Platform.Application.Interfaces.Services;
using DS.Platform.Shared.Wrapper;
using MediatR;

namespace DS.Platform.Application.Features.Audits.Queries.GetCurrentUserTrails
{
    /// <summary>The current user's latest 250 audit trail rows (written by the DbContext on every save).</summary>
    public class GetCurrentUserAuditTrailsQuery : IRequest<Result<IEnumerable<AuditResponse>>>
    {
    }

    internal class GetCurrentUserAuditTrailsQueryHandler(
        IUnitOfWork<int> unitOfWork,
        ICurrentUserService currentUserService) : IRequestHandler<GetCurrentUserAuditTrailsQuery, Result<IEnumerable<AuditResponse>>>
    {
        private const int MaxRows = 250;

        public async Task<Result<IEnumerable<AuditResponse>>> Handle(GetCurrentUserAuditTrailsQuery request, CancellationToken cancellationToken)
        {
            try
            {
                var trails = await unitOfWork.AuditTrails.GetLatestByUserAsync(currentUserService.UserId!, MaxRows, cancellationToken);
                var mappedLogs = trails.Select(x => x.ToResponse()).ToList();
                return await Result<IEnumerable<AuditResponse>>.SuccessAsync(mappedLogs);
            }
            catch (Exception)
            {
                return await Result<IEnumerable<AuditResponse>>.FailAsync(string.Format("Algo salió mal."));
            }
        }
    }
}
