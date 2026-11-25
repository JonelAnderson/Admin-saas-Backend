using DS.Platform.Application.DTOs.Plans.Request;
using DS.Platform.Application.Interfaces.Repositories;
using DS.Platform.Application.Mappings.Plans;
using DS.Platform.Shared.Localization;
using DS.Platform.Shared.Wrapper;
using MediatR;
using Microsoft.Extensions.Localization;

namespace DS.Platform.Application.Features.Plans.Commands.Create
{
    public class CreatePlanCommand : IRequest<Result<int>>
    {
        public PlanRequestDto? PlanRequestDto { get; set; }
    }

    internal class CreatePlanCommandHandler(
        IUnitOfWork<int> unitOfWork,
        IStringLocalizer<ServerCommonResources> localizer) : IRequestHandler<CreatePlanCommand, Result<int>>
    {
        public async Task<Result<int>> Handle(CreatePlanCommand command, CancellationToken cancellationToken)
        {
            var dto = command.PlanRequestDto!;

            if (!await unitOfWork.Plans.IsUniqueCodeAsync(dto.Product, dto.Code, null, cancellationToken))
            {
                return await Result<int>.FailAsync(DuplicateCodeMessage(dto.Code));
            }

            var plan = dto.ToEntity();
            await unitOfWork.Plans.AddAsync(plan);
            await unitOfWork.Commit(cancellationToken);

            return await Result<int>.SuccessAsync(plan.Id, localizer["MessageSave"]);
        }

        /// <summary>Mensaje del código repetido (lo comparten crear y editar).</summary>
        internal static string DuplicateCodeMessage(string code)
            => $"Ya existe un plan con el código {PlanMappings.NormalizeCode(code)} para ese producto.";
    }
}
