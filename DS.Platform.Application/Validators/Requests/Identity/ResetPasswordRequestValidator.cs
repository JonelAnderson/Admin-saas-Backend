using DS.Platform.Application.DTOs.Identities.Request;
using FluentValidation;

namespace DS.Platform.Application.Validators.Requests.Identity
{
    public class ResetPasswordRequestValidator : AbstractValidator<ResetPasswordRequest>
    {
        public ResetPasswordRequestValidator()
        {
            RuleFor(request => request.Email)
                .Must(x => !string.IsNullOrWhiteSpace(x)).WithMessage(x => "Email is required")
                .EmailAddress().WithMessage(x => "Email is not correct");
            RuleFor(request => request.Password).ErpPassword();
            RuleFor(request => request.ConfirmPassword)
                .Must(x => !string.IsNullOrWhiteSpace(x)).WithMessage(x =>"Password Confirmation is required!")
                .Equal(request => request.Password).WithMessage(x => "Passwords don't match");
            RuleFor(request => request.Token)
                .Must(x => !string.IsNullOrWhiteSpace(x)).WithMessage(x => "Token is required");
        }
    }
}
