using DS.Platform.Application.Interfaces.Repositories;
using DS.Platform.Shared.Localization;
using DS.Platform.Shared.Wrapper;
using MediatR;
using Microsoft.Extensions.Localization;

namespace DS.Platform.Application.Features.Plans.Commands.Delete
{
    /// <summary>Borrado lógico de un plan (libera su código).</summary>
    public class DeletePlanCommand : IRequest<Result<int>>
    {
        public int Id { get; set; }
    }

    internal class DeletePlanCommandHandler(
        IUnitOfWork<int> unitOfWork,
        IStringLocalizer<ServerCommonResources> localizer) : IRequestHandler<DeletePlanCommand, Result<int>>
    {
        public const string InUseMessage = "El plan tiene suscripciones abiertas: desactívelo en lugar de eliminarlo.";

        public async Task<Result<int>> Handle(DeletePlanCommand request, CancellationToken cancellationToken)
        {
            var plan = await unitOfWork.Plans.GetByIdAsync(request.Id);
            if (plan is null || plan.IsDeleted)
            {
                return await Result<int>.FailAsync(localizer["MessageQueryEmpty"]);
            }

            // Un plan con suscripciones abiertas no se borra: se desactiva (deja de ofrecerse y las suscripciones siguen).
            if (await unitOfWork.Subscriptions.PlanHasOpenAsync(plan.Id, cancellationToken))
            {
                return await Result<int>.FailAsync(InUseMessage);
            }

            await unitOfWork.Plans.DeleteAsync(plan);
            await unitOfWork.Commit(cancellationToken);

            return await Result<int>.SuccessAsync(plan.Id, localizer["MessageDelete"]);
        }
    }
}
