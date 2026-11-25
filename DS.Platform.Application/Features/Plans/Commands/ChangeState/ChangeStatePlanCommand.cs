using DS.Platform.Application.Interfaces.Repositories;
using DS.Platform.Shared.Localization;
using DS.Platform.Shared.Wrapper;
using MediatR;
using Microsoft.Extensions.Localization;

namespace DS.Platform.Application.Features.Plans.Commands.ChangeState
{
    /// <summary>Activa o desactiva un plan: uno inactivo no se ofrece en los combos (las suscripciones que ya lo usan siguen).</summary>
    public class ChangeStatePlanCommand : IRequest<Result<int>>
    {
        public int Id { get; set; }
    }

    internal class ChangeStatePlanCommandHandler(
        IUnitOfWork<int> unitOfWork,
        IStringLocalizer<ServerCommonResources> localizer) : IRequestHandler<ChangeStatePlanCommand, Result<int>>
    {
        public async Task<Result<int>> Handle(ChangeStatePlanCommand request, CancellationToken cancellationToken)
        {
            var plan = await unitOfWork.Plans.GetByIdAsync(request.Id);
            if (plan is null || plan.IsDeleted)
            {
                return await Result<int>.FailAsync(localizer["MessageQueryEmpty"]);
            }

            plan.IsActive = !plan.IsActive;
            await unitOfWork.Plans.UpdateAsync(plan);
            await unitOfWork.Commit(cancellationToken);

            return await Result<int>.SuccessAsync(plan.Id, localizer["MessageUpdate"]);
        }
    }
}
