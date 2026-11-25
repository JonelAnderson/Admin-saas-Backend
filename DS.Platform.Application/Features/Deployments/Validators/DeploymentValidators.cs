using DS.Platform.Application.Features.Deployments.Commands.Provision;
using DS.Platform.Application.Features.Deployments.Commands.Upgrade;
using FluentValidation;

namespace DS.Platform.Application.Features.Deployments.Validators
{
    public class ProvisionInstanceCommandValidator : AbstractValidator<ProvisionInstanceCommand>
    {
        public ProvisionInstanceCommandValidator()
        {
            RuleFor(x => x.ProvisionInstanceRequestDto).NotNull();
            When(x => x.ProvisionInstanceRequestDto is not null, () =>
            {
                RuleFor(x => x.ProvisionInstanceRequestDto!.InstanceId).GreaterThan(0);
                RuleFor(x => x.ProvisionInstanceRequestDto!.Subdomain).NotEmpty().MaximumLength(30);
                RuleFor(x => x.ProvisionInstanceRequestDto!.Version).NotEmpty().MaximumLength(40);
            });
        }
    }

    public class UpgradeInstanceCommandValidator : AbstractValidator<UpgradeInstanceCommand>
    {
        public UpgradeInstanceCommandValidator()
        {
            RuleFor(x => x.UpgradeInstanceRequestDto).NotNull();
            When(x => x.UpgradeInstanceRequestDto is not null, () =>
            {
                RuleFor(x => x.UpgradeInstanceRequestDto!.InstanceId).GreaterThan(0);
                RuleFor(x => x.UpgradeInstanceRequestDto!.Version).NotEmpty().MaximumLength(40);
            });
        }
    }
}
