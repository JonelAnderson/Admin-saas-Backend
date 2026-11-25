using DS.Platform.Application.Features.ApiSunat.Common;
using DS.Platform.Application.Features.ApiSunat.Commands.Link;
using DS.Platform.Application.Interfaces.Repositories;
using DS.Platform.Application.Interfaces.Services;
using DS.Platform.Application.Interfaces.Services.ApiSunat;
using DS.Platform.Application.Mappings.ApiSunat;
using DS.Platform.Shared.Localization;
using DS.Platform.Shared.Wrapper;
using MediatR;
using Microsoft.Extensions.Localization;

namespace DS.Platform.Application.Features.ApiSunat.Commands.Sync
{
    /// <summary>
    /// Trae de la API SUNAT los datos y el uso del mes de la empresa. También corrige el desvío: si la API no está como la consola
    /// quiere y no hay nada en camino, encola la acción para converger.
    /// </summary>
    public class SyncIssuerCommand : IRequest<Result<int>>
    {
        public int Id { get; set; }
    }

    internal class SyncIssuerCommandHandler(
        IUnitOfWork<int> unitOfWork,
        IApiSunatAdminClient apiSunat,
        IDateTimeService dateTime,
        IStringLocalizer<ServerCommonResources> localizer) : IRequestHandler<SyncIssuerCommand, Result<int>>
    {
        public async Task<Result<int>> Handle(SyncIssuerCommand request, CancellationToken cancellationToken)
        {
            var issuer = await unitOfWork.ApiSunatIssuers.GetByIdAsync(request.Id);
            if (issuer is null || issuer.IsDeleted)
            {
                return await Result<int>.FailAsync(localizer["MessageQueryEmpty"]);
            }

            var call = await apiSunat.GetTenantAsync(issuer.TenantId, cancellationToken);
            if (!call.Success)
            {
                return await Result<int>.FailAsync(call.StatusCode == 404
                    ? LinkIssuerCommandHandler.TenantNotFoundMessage
                    : LinkIssuerCommandHandler.ApiUnavailableMessage(call.Error));
            }

            var now = dateTime.NowUtc;
            var tenant = call.Data!;
            tenant.ApplyTo(issuer, now);
            issuer.SuspendApplied = ApiAccessPolicy.AppliedFor(tenant, issuer.SuspendDesired);
            await unitOfWork.ApiSunatIssuers.UpdateAsync(issuer);

            var subscriptions = await unitOfWork.Subscriptions.GetByCustomerAsync(issuer.CustomerId, cancellationToken);
            await ApiAccessReconciler.ReconcileIssuerAsync(unitOfWork, issuer, subscriptions, now, cancellationToken);

            await unitOfWork.Commit(cancellationToken);

            return await Result<int>.SuccessAsync(issuer.Id, localizer["MessageUpdate"]);
        }
    }
}
