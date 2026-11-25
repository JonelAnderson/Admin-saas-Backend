using DS.Platform.Application.DTOs.Subscriptions.Request;
using DS.Platform.Application.Helpers;
using DS.Platform.Application.Features.ApiSunat.Common;
using DS.Platform.Application.Interfaces.Repositories;
using DS.Platform.Application.Interfaces.Services;
using DS.Platform.Application.Mappings.Plans;
using DS.Platform.Domain.Entities.Billing;
using DS.Platform.Domain.Enums;
using DS.Platform.Shared.Localization;
using DS.Platform.Shared.Wrapper;
using MediatR;
using Microsoft.Extensions.Localization;

namespace DS.Platform.Application.Features.Subscriptions.Commands.Create
{
    /// <summary>Un cliente contrata un plan (ERP o API SUNAT).</summary>
    public class CreateSubscriptionCommand : IRequest<Result<int>>
    {
        public CreateSubscriptionRequestDto? CreateSubscriptionRequestDto { get; set; }
    }

    internal class CreateSubscriptionCommandHandler(
        IUnitOfWork<int> unitOfWork,
        IDateTimeService dateTime,
        IStringLocalizer<ServerCommonResources> localizer) : IRequestHandler<CreateSubscriptionCommand, Result<int>>
    {
        public const string CustomerNotFoundMessage = "El cliente no existe.";
        public const string CustomerInactiveMessage = "El cliente está inactivo: actívelo antes de suscribirlo.";
        public const string PlanNotFoundMessage = "El plan no existe.";
        public const string PlanInactiveMessage = "El plan está inactivo: elija un plan activo.";
        public const string NoYearlyPriceMessage = "El plan no se vende anual (no tiene precio anual).";
        public const string CancelledMessage = "La suscripción está cancelada: no se puede modificar.";

        public static string AlreadySubscribedMessage(PlanProduct product)
            => $"El cliente ya tiene una suscripción de {(product == PlanProduct.Erp ? PlanMappings.ErpName : PlanMappings.ApiSunatName)}: "
               + "cámbiele el plan o cancélela antes de crear otra.";

        public async Task<Result<int>> Handle(CreateSubscriptionCommand command, CancellationToken cancellationToken)
        {
            var dto = command.CreateSubscriptionRequestDto!;

            var customer = await unitOfWork.Customers.GetByIdAsync(dto.CustomerId);
            if (customer is null || customer.IsDeleted)
            {
                return await Result<int>.FailAsync(CustomerNotFoundMessage);
            }
            if (!customer.IsActive)
            {
                return await Result<int>.FailAsync(CustomerInactiveMessage);
            }

            var plan = await unitOfWork.Plans.GetByIdAsync(dto.PlanId);
            if (plan is null || plan.IsDeleted)
            {
                return await Result<int>.FailAsync(PlanNotFoundMessage);
            }
            if (!plan.IsActive)
            {
                return await Result<int>.FailAsync(PlanInactiveMessage);
            }
            if (dto.BillingCycle == BillingCycle.Yearly && plan.YearlyPrice is null)
            {
                return await Result<int>.FailAsync(NoYearlyPriceMessage);
            }
            if (await unitOfWork.Subscriptions.HasOpenAsync(customer.Id, plan.Product, null, cancellationToken))
            {
                return await Result<int>.FailAsync(AlreadySubscribedMessage(plan.Product));
            }

            var today = LimaDate.Today(dateTime.NowUtc);
            var startOn = dto.StartOn ?? today;
            // La prueba gratuita se da una sola vez por cliente y producto (también si la anterior se canceló o se borró).
            var trialUsed = await unitOfWork.Subscriptions.EverSubscribedIncludingDeletedAsync(customer.Id, plan.Product, cancellationToken);

            var subscription = new Subscription
            {
                CustomerId = customer.Id,
                PlanId = plan.Id,
                Plan = plan,
                Product = plan.Product,
                BillingCycle = dto.BillingCycle,
                Price = dto.Price ?? PriceFor(plan, dto.BillingCycle),
                Currency = plan.Currency,
                StartOn = startOn,
                TrialEndsOn = plan.TrialDays > 0 && !trialUsed ? startOn.AddDays(plan.TrialDays - 1) : null,
                BillingDay = dto.BillingDay ?? Math.Min(startOn.Day, 28),
                GraceDays = dto.GraceDays,
                Notes = string.IsNullOrWhiteSpace(dto.Notes) ? null : dto.Notes.Trim()
            };
            SubscriptionStatusCalculator.Apply(subscription, today);

            await unitOfWork.Subscriptions.AddAsync(subscription);
            // La suscripción nueva aún no está guardada: se pasa aparte para que el reconciliador la vea.
            await ApiAccessReconciler.ReconcileAsync(unitOfWork, customer.Id, dateTime.NowUtc, cancellationToken, subscription);
            await unitOfWork.Commit(cancellationToken);

            return await Result<int>.SuccessAsync(subscription.Id, localizer["MessageSave"]);
        }

        /// <summary>Precio del plan para el ciclo (anual si el plan lo vende; si no, el mensual).</summary>
        internal static decimal PriceFor(Plan plan, BillingCycle cycle)
            => cycle == BillingCycle.Yearly && plan.YearlyPrice is { } yearly ? yearly : plan.MonthlyPrice;

        /// <summary>
        /// Carga una suscripción para modificarla (la comparten editar, prorrogar, suspender, reactivar y cancelar). Devuelve el
        /// mensaje de error si no existe o está cancelada.
        /// </summary>
        internal static async Task<(Subscription? Subscription, string? Error)> LoadOpenAsync(
            IUnitOfWork<int> unitOfWork, int id, IStringLocalizer<ServerCommonResources> localizer)
        {
            var subscription = await unitOfWork.Subscriptions.GetByIdAsync(id);
            if (subscription is null || subscription.IsDeleted)
            {
                return (null, localizer["MessageQueryEmpty"]);
            }
            if (subscription.Status == SubscriptionStatus.Cancelled)
            {
                return (null, CancelledMessage);
            }
            return (subscription, null);
        }
    }
}
