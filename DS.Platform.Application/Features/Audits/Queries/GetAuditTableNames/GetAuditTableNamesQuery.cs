using DS.Platform.Application.Features.Audits.Queries.GetAuditTrails;
using DS.Platform.Application.Interfaces.Repositories;
using DS.Platform.Application.Interfaces.Services;
using DS.Platform.Shared.Wrapper;
using MediatR;

namespace DS.Platform.Application.Features.Audits.Queries.GetAuditTableNames
{
    /// <summary>Las entidades que aparecen en la pista de auditoría (filtro de la pantalla). Solo SuperAdmin.</summary>
    public class GetAuditTableNamesQuery : IRequest<Result<IEnumerable<string>>>
    {
    }

    internal class GetAuditTableNamesQueryHandler(
        IUnitOfWork<int> unitOfWork,
        ICurrentUserService currentUserService) : IRequestHandler<GetAuditTableNamesQuery, Result<IEnumerable<string>>>
    {
        public async Task<Result<IEnumerable<string>>> Handle(GetAuditTableNamesQuery request, CancellationToken cancellationToken)
        {
            if (!currentUserService.IsPlatformAdmin)
                return await Result<IEnumerable<string>>.FailAsync(GetAuditTrailsQueryHandler.OnlyPlatformAdminMessage);

            var tables = await unitOfWork.AuditTrails.TableNamesAsync(cancellationToken);
            return await Result<IEnumerable<string>>.SuccessAsync(tables);
        }
    }
}
