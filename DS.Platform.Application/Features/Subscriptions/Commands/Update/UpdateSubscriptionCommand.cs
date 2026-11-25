using DS.Platform.Application.DTOs.Subscriptions.Request;
using DS.Platform.Application.Features.Subscriptions.Commands.Create;
using DS.Platform.Application.Helpers;
using DS.Platform.Application.Features.ApiSunat.Common;
using DS.Platform.Application.Interfaces.Repositories;
using DS.Platform.Application.Interfaces.Services;
using DS.Platform.Domain.Enums;
using DS.Platform.Shared.Localization;
using DS.Platform.Shared.Wrapper;
using MediatR;
using Microsoft.Extensions.Localization;

namespace DS.Platform.Application.Features.Subscriptions.Commands.Update
{
    /// <summary>Cambio de plan (del mismo producto), ciclo, precio pactado, día de vencimiento, gracia y notas.</summary>
    public class UpdateSubscriptionCommand : IRequest<Result<int>>
    {
        public UpdateSubscriptionRequestDto? UpdateSubscriptionRequestDto { get; set; }
    }

    internal class UpdateSubscriptionCommandHandler(
        IUnitOfWork<int> unitOfWork,
        IDateTimeService dateTime,
        IStringLocalizer<ServerCommonResources> localizer) : IRequestHandler<UpdateSubscriptionCommand, Result<int>>
    {
        public const string OtherProductMessage =
            "El plan nuevo es de otro producto: cancele esta suscripción y cree una nueva del otro producto.";

        public async Task<Result<int>> Handle(UpdateSubscriptionCommand command, CancellationToken cancellationToken)
        {
            var dto = command.UpdateSubscriptionRequestDto!;

            var (subscription, error) = await CreateSubscriptionCommandHandler.LoadOpenAsync(unitOfWork, dto.Id, localizer);
            if (subscription is null)
            {
                return await Result<int>.FailAsync(error!);
            }

            if (dto.PlanId != subscription.PlanId)
            {
                var plan = await unitOfWork.Plans.GetByIdAsync(dto.PlanId);
                if (plan is null || plan.IsDeleted)
                {
                    return await Result<int>.FailAsync(CreateSubscriptionCommandHandler.PlanNotFoundMessage);
                }
                if (!plan.IsActive)
                {
                    return await Result<int>.FailAsync(CreateSubscriptionCommandHandler.PlanInactiveMessage);
                }
                if (plan.Product != subscription.Product)
                {
                    return await Result<int>.FailAsync(OtherProductMessage);
                }
                subscription.PlanId = plan.Id;
                subscription.Plan = plan;
                subscription.Currency = plan.Currency;
            }

            if (dto.BillingCycle == BillingCycle.Yearly && dto.BillingCycle != subscription.BillingCycle)
            {
                var plan = await unitOfWork.Plans.GetByIdAsync(subscription.PlanId);
                if (plan?.YearlyPrice is null)
                {
                    return await Result<int>.FailAsync(CreateSubscriptionCommandHandler.NoYearlyPriceMessage);
                }
            }

            subscription.BillingCycle = dto.BillingCycle;
            subscription.Price = dto.Price;
            subscription.BillingDay = dto.BillingDay;
            subscription.GraceDays = dto.GraceDays;
            subscription.Notes = string.IsNullOrWhiteSpace(dto.Notes) ? null : dto.Notes.Trim();
            SubscriptionStatusCalculator.Apply(subscription, LimaDate.Today(dateTime.NowUtc));

            await unitOfWork.Subscriptions.UpdateAsync(subscription);
            // El corte efectivo llega a la API SUNAT por la bandeja de acciones remotas, en este mismo guardado.
            await ApiAccessReconciler.ReconcileAsync(unitOfWork, subscription.CustomerId, dateTime.NowUtc, cancellationToken);
            await unitOfWork.Commit(cancellationToken);

            return await Result<int>.SuccessAsync(subscription.Id, localizer["MessageUpdate"]);
        }
    }
}
