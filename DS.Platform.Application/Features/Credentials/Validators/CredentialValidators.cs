using DS.Platform.Application.Features.Credentials.Commands.Ack;
using DS.Platform.Application.Features.Credentials.Commands.Create;
using DS.Platform.Application.Features.Credentials.Commands.Pull;
using DS.Platform.Application.Features.Credentials.Common;
using FluentValidation;

namespace DS.Platform.Application.Features.Credentials.Validators
{
    public class CreateApiCredentialDeliveryCommandValidator : AbstractValidator<CreateApiCredentialDeliveryCommand>
    {
        public CreateApiCredentialDeliveryCommandValidator()
        {
            RuleFor(x => x.CreateApiCredentialDeliveryRequestDto).NotNull();
            When(x => x.CreateApiCredentialDeliveryRequestDto is not null, () =>
            {
                RuleFor(x => x.CreateApiCredentialDeliveryRequestDto!.InstanceId).GreaterThan(0);
                RuleFor(x => x.CreateApiCredentialDeliveryRequestDto!.IssuerId).GreaterThan(0);
            });
        }
    }

    /// <summary>La instancia consulta con un nonce de 16 a 64 caracteres, como en la consulta de licencia.</summary>
    public class PullApiSunatCredentialsCommandValidator : AbstractValidator<PullApiSunatCredentialsCommand>
    {
        public PullApiSunatCredentialsCommandValidator()
        {
            RuleFor(x => x.ApiSunatPullRequestDto).NotNull();
            When(x => x.ApiSunatPullRequestDto is not null, () =>
            {
                RuleFor(x => x.ApiSunatPullRequestDto!.Nonce).NotEmpty().Length(16, 64).Matches("^[A-Za-z0-9_-]+$");
            });
        }
    }

    public class AckApiSunatCredentialsCommandValidator : AbstractValidator<AckApiSunatCredentialsCommand>
    {
        public AckApiSunatCredentialsCommandValidator()
        {
            RuleFor(x => x.ApiSunatAckRequestDto).NotNull();
            When(x => x.ApiSunatAckRequestDto is not null, () =>
            {
                RuleFor(x => x.ApiSunatAckRequestDto!.Ids).NotNull().NotEmpty()
                    .Must(ids => ids!.Count <= CredentialDeliveryRules.MaxPerAck)
                    .WithMessage($"Confirme hasta {CredentialDeliveryRules.MaxPerAck} entregas por llamada.");
                RuleForEach(x => x.ApiSunatAckRequestDto!.Ids).GreaterThan(0);
            });
        }
    }
}
