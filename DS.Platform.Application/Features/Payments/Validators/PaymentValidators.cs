using DS.Platform.Application.Features.Payments.Commands.Register;
using DS.Platform.Application.Features.Payments.Commands.Void;
using FluentValidation;

namespace DS.Platform.Application.Features.Payments.Validators
{
    public class RegisterPaymentCommandValidator : AbstractValidator<RegisterPaymentCommand>
    {
        public const int MaxPeriods = 24;

        public RegisterPaymentCommandValidator()
        {
            RuleFor(x => x.RegisterPaymentRequestDto).NotNull();
            When(x => x.RegisterPaymentRequestDto is not null, () =>
            {
                RuleFor(x => x.RegisterPaymentRequestDto!.SubscriptionId).GreaterThan(0);
                RuleFor(x => x.RegisterPaymentRequestDto!.Amount).GreaterThan(0).PrecisionScale(12, 2, true);
                RuleFor(x => x.RegisterPaymentRequestDto!.Currency).NotEmpty().Length(3);
                RuleFor(x => x.RegisterPaymentRequestDto!.Method).IsInEnum();
                RuleFor(x => x.RegisterPaymentRequestDto!.Periods).InclusiveBetween(1, MaxPeriods);
                RuleFor(x => x.RegisterPaymentRequestDto!.OperationNumber).MaximumLength(60);
                RuleFor(x => x.RegisterPaymentRequestDto!.Notes).MaximumLength(1000);
                RuleFor(x => x.RegisterPaymentRequestDto!.PaidOn).GreaterThanOrEqualTo(new DateOnly(2020, 1, 1))
                    .When(x => x.RegisterPaymentRequestDto!.PaidOn.HasValue);
            });
        }
    }

    public class VoidPaymentCommandValidator : AbstractValidator<VoidPaymentCommand>
    {
        public VoidPaymentCommandValidator()
        {
            RuleFor(x => x.VoidPaymentRequestDto).NotNull();
            RuleFor(x => x.VoidPaymentRequestDto!.Id).GreaterThan(0).When(x => x.VoidPaymentRequestDto is not null);
            RuleFor(x => x.VoidPaymentRequestDto!.Reason).NotEmpty().MaximumLength(400)
                .WithMessage("Indique el motivo de la anulación.").When(x => x.VoidPaymentRequestDto is not null);
        }
    }
}
