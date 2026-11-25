using DS.Platform.Application.Features.Subscriptions.Commands.Cancel;
using DS.Platform.Application.Features.Subscriptions.Commands.Create;
using DS.Platform.Application.Features.Subscriptions.Commands.Extend;
using DS.Platform.Application.Features.Subscriptions.Commands.Reactivate;
using DS.Platform.Application.Features.Subscriptions.Commands.Suspend;
using DS.Platform.Application.Features.Subscriptions.Commands.Update;
using FluentValidation;

namespace DS.Platform.Application.Features.Subscriptions.Validators
{
    internal static class SubscriptionLimits
    {
        public const int MaxGraceDays = 30;
        public const int MaxReason = 400;
        public static readonly DateOnly MinStart = new(2020, 1, 1);
    }

    public class CreateSubscriptionCommandValidator : AbstractValidator<CreateSubscriptionCommand>
    {
        public CreateSubscriptionCommandValidator()
        {
            RuleFor(x => x.CreateSubscriptionRequestDto).NotNull();
            When(x => x.CreateSubscriptionRequestDto is not null, () =>
            {
                RuleFor(x => x.CreateSubscriptionRequestDto!.CustomerId).GreaterThan(0);
                RuleFor(x => x.CreateSubscriptionRequestDto!.PlanId).GreaterThan(0);
                RuleFor(x => x.CreateSubscriptionRequestDto!.BillingCycle).IsInEnum();
                RuleFor(x => x.CreateSubscriptionRequestDto!.StartOn).GreaterThanOrEqualTo(SubscriptionLimits.MinStart)
                    .When(x => x.CreateSubscriptionRequestDto!.StartOn.HasValue);
                RuleFor(x => x.CreateSubscriptionRequestDto!.Price).GreaterThanOrEqualTo(0).PrecisionScale(12, 2, true)
                    .When(x => x.CreateSubscriptionRequestDto!.Price.HasValue);
                RuleFor(x => x.CreateSubscriptionRequestDto!.BillingDay).InclusiveBetween(1, 28)
                    .When(x => x.CreateSubscriptionRequestDto!.BillingDay.HasValue);
                RuleFor(x => x.CreateSubscriptionRequestDto!.GraceDays).InclusiveBetween(0, SubscriptionLimits.MaxGraceDays);
                RuleFor(x => x.CreateSubscriptionRequestDto!.Notes).MaximumLength(1000);
            });
        }
    }

    public class UpdateSubscriptionCommandValidator : AbstractValidator<UpdateSubscriptionCommand>
    {
        public UpdateSubscriptionCommandValidator()
        {
            RuleFor(x => x.UpdateSubscriptionRequestDto).NotNull();
            When(x => x.UpdateSubscriptionRequestDto is not null, () =>
            {
                RuleFor(x => x.UpdateSubscriptionRequestDto!.Id).GreaterThan(0);
                RuleFor(x => x.UpdateSubscriptionRequestDto!.PlanId).GreaterThan(0);
                RuleFor(x => x.UpdateSubscriptionRequestDto!.BillingCycle).IsInEnum();
                RuleFor(x => x.UpdateSubscriptionRequestDto!.Price).GreaterThanOrEqualTo(0).PrecisionScale(12, 2, true);
                RuleFor(x => x.UpdateSubscriptionRequestDto!.BillingDay).InclusiveBetween(1, 28);
                RuleFor(x => x.UpdateSubscriptionRequestDto!.GraceDays).InclusiveBetween(0, SubscriptionLimits.MaxGraceDays);
                RuleFor(x => x.UpdateSubscriptionRequestDto!.Notes).MaximumLength(1000);
            });
        }
    }

    public class ExtendSubscriptionCommandValidator : AbstractValidator<ExtendSubscriptionCommand>
    {
        public ExtendSubscriptionCommandValidator()
        {
            RuleFor(x => x.ExtendSubscriptionRequestDto).NotNull();
            When(x => x.ExtendSubscriptionRequestDto is not null, () =>
            {
                RuleFor(x => x.ExtendSubscriptionRequestDto!.Id).GreaterThan(0);
                RuleFor(x => x.ExtendSubscriptionRequestDto!.Reason).NotEmpty().MaximumLength(SubscriptionLimits.MaxReason)
                    .WithMessage("Indique el motivo de la prórroga.");
            });
        }
    }

    // Suspender, reactivar y cancelar: id y motivo obligatorio.
    public class SuspendSubscriptionCommandValidator : AbstractValidator<SuspendSubscriptionCommand>
    {
        public SuspendSubscriptionCommandValidator()
        {
            RuleFor(x => x.SubscriptionReasonRequestDto).NotNull();
            RuleFor(x => x.SubscriptionReasonRequestDto!.Id).GreaterThan(0).When(x => x.SubscriptionReasonRequestDto is not null);
            RuleFor(x => x.SubscriptionReasonRequestDto!.Reason).NotEmpty().MaximumLength(SubscriptionLimits.MaxReason)
                .WithMessage("Indique el motivo de la suspensión.").When(x => x.SubscriptionReasonRequestDto is not null);
        }
    }

    public class ReactivateSubscriptionCommandValidator : AbstractValidator<ReactivateSubscriptionCommand>
    {
        public ReactivateSubscriptionCommandValidator()
        {
            RuleFor(x => x.SubscriptionReasonRequestDto).NotNull();
            RuleFor(x => x.SubscriptionReasonRequestDto!.Id).GreaterThan(0).When(x => x.SubscriptionReasonRequestDto is not null);
            RuleFor(x => x.SubscriptionReasonRequestDto!.Reason).NotEmpty().MaximumLength(SubscriptionLimits.MaxReason)
                .WithMessage("Indique el motivo de la reactivación.").When(x => x.SubscriptionReasonRequestDto is not null);
        }
    }

    public class CancelSubscriptionCommandValidator : AbstractValidator<CancelSubscriptionCommand>
    {
        public CancelSubscriptionCommandValidator()
        {
            RuleFor(x => x.SubscriptionReasonRequestDto).NotNull();
            RuleFor(x => x.SubscriptionReasonRequestDto!.Id).GreaterThan(0).When(x => x.SubscriptionReasonRequestDto is not null);
            RuleFor(x => x.SubscriptionReasonRequestDto!.Reason).NotEmpty().MaximumLength(SubscriptionLimits.MaxReason)
                .WithMessage("Indique el motivo de la cancelación.").When(x => x.SubscriptionReasonRequestDto is not null);
        }
    }
}
