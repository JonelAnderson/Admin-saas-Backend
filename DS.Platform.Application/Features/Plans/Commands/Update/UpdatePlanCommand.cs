using DS.Platform.Application.DTOs.Plans.Request;
using DS.Platform.Application.Features.ApiSunat.Common;
using DS.Platform.Application.Features.Plans.Commands.Create;
using DS.Platform.Application.Interfaces.Repositories;
using DS.Platform.Application.Interfaces.Services;
using DS.Platform.Application.Mappings.Plans;
using DS.Platform.Shared.Localization;
using DS.Platform.Shared.Wrapper;
using MediatR;
using Microsoft.Extensions.Localization;

namespace DS.Platform.Application.Features.Plans.Commands.Update
{
    public class UpdatePlanCommand : IRequest<Result<int>>
    {
        public UpdatePlanRequestDto? UpdatePlanRequestDto { get; set; }
    }

    internal class UpdatePlanCommandHandler(
        IUnitOfWork<int> unitOfWork,
        IDateTimeService dateTime,
        IStringLocalizer<ServerCommonResources> localizer) : IRequestHandler<UpdatePlanCommand, Result<int>>
    {
        public const string ProductInUseMessage = "El plan tiene suscripciones abiertas: no se puede cambiar su producto.";

        public async Task<Result<int>> Handle(UpdatePlanCommand command, CancellationToken cancellationToken)
        {
            var dto = command.UpdatePlanRequestDto!;

            var plan = await unitOfWork.Plans.GetByIdAsync(dto.Id);
            if (plan is null || plan.IsDeleted)
            {
                return await Result<int>.FailAsync(localizer["MessageQueryEmpty"]);
            }

            if (!await unitOfWork.Plans.IsUniqueCodeAsync(dto.Product, dto.Code, dto.Id, cancellationToken))
            {
                return await Result<int>.FailAsync(CreatePlanCommandHandler.DuplicateCodeMessage(dto.Code));
            }

            // Un plan en uso no cambia de producto: sus suscripciones quedarían con límites de otro producto.
            if (dto.Product != plan.Product && await unitOfWork.Subscriptions.PlanHasOpenAsync(plan.Id, cancellationToken))
            {
                return await Result<int>.FailAsync(ProductInUseMessage);
            }

            dto.ApplyTo(plan);

            await unitOfWork.Plans.UpdateAsync(plan);

            // Si cambió su plan de la API o su cupo, las empresas de sus clientes se reajustan (bandeja de acciones remotas, mismo Commit).
            foreach (var customerId in await unitOfWork.Subscriptions.GetCustomerIdsByPlanAsync(plan.Id, cancellationToken))
            {
                await ApiAccessReconciler.ReconcileAsync(unitOfWork, customerId, dateTime.NowUtc, cancellationToken);
            }
            await unitOfWork.Commit(cancellationToken);

            return await Result<int>.SuccessAsync(plan.Id, localizer["MessageUpdate"]);
        }
    }
}
