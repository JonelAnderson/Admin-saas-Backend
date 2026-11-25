using DS.Platform.Application.DTOs.Identities.Request;
using FluentValidation;

namespace DS.Platform.Application.Validators.Requests.Identity
{
    public class ChangePasswordRequestValidator : AbstractValidator<ChangePasswordRequest>
    {
        public ChangePasswordRequestValidator()
        {
            RuleFor(request => request.Password)
                .Must(x => !string.IsNullOrWhiteSpace(x)).WithMessage(x => "Current Password is required!");
#pragma warning disable CS8620 // NewPassword is non-nullable; the shared rule accepts string?
            RuleFor(request => request.NewPassword).ErpPassword();
#pragma warning restore CS8620
            RuleFor(request => request.ConfirmNewPassword)
                .Must(x => !string.IsNullOrWhiteSpace(x)).WithMessage(x => "Password Confirmation is required!")
                .Equal(request => request.NewPassword).WithMessage(x => "Passwords don't match");
        }
    }
}