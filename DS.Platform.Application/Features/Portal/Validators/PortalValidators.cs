using DS.Platform.Application.Features.Portal.Commands.Invite;
using FluentValidation;

namespace DS.Platform.Application.Features.Portal.Validators
{
    public class InviteToPortalCommandValidator : AbstractValidator<InviteToPortalCommand>
    {
        public InviteToPortalCommandValidator()
        {
            RuleFor(x => x.InviteToPortalRequestDto).NotNull();
            When(x => x.InviteToPortalRequestDto is not null, () =>
            {
                RuleFor(x => x.InviteToPortalRequestDto!.CustomerId).GreaterThan(0);
                RuleFor(x => x.InviteToPortalRequestDto!.Email).NotEmpty().MaximumLength(200).EmailAddress();
                RuleFor(x => x.InviteToPortalRequestDto!.Name).NotEmpty().MaximumLength(120);
                RuleFor(x => x.InviteToPortalRequestDto!.IssuerIds).NotNull().NotEmpty().Must(ids => ids.Count <= 50)
                    .WithMessage("Elija entre 1 y 50 empresas.");
            });
        }
    }
}
