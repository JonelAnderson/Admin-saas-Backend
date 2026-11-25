using DS.Platform.Application.Features.ApiSunat.Commands.Link;
using FluentValidation;

namespace DS.Platform.Application.Features.ApiSunat.Validators
{
    public class LinkIssuerCommandValidator : AbstractValidator<LinkIssuerCommand>
    {
        public LinkIssuerCommandValidator()
        {
            RuleFor(x => x.LinkIssuerRequestDto).NotNull();
            When(x => x.LinkIssuerRequestDto is not null, () =>
            {
                RuleFor(x => x.LinkIssuerRequestDto!.CustomerId).GreaterThan(0);
                RuleFor(x => x.LinkIssuerRequestDto!.TenantId).GreaterThan(0);
                RuleFor(x => x.LinkIssuerRequestDto!.Notes).MaximumLength(1000);
            });
        }
    }
}
