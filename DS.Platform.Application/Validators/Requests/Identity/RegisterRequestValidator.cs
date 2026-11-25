using DS.Platform.Application.DTOs.Identities.Request;
using FluentValidation;

namespace DS.Platform.Application.Validators.Requests.Identity
{
    public class RegisterRequestValidator : AbstractValidator<RegisterRequest>
    {
        public RegisterRequestValidator()
        {

            RuleFor(request => request.FirstName).Length(4, 255).WithMessage("The {PropertyName} has {TotalLength} characters. Must be between {MinLength} and {MaxLength} characters.")
               .NotEmpty().WithMessage("{PropertyName} is required")
               .NotNull().WithMessage("{PropertyName} is required");


            RuleFor(request => request.LastName).Length(4, 255).WithMessage("The {PropertyName} has {TotalLength} characters. Must be between {MinLength} and {MaxLength} characters.")
               .NotEmpty().WithMessage("{PropertyName} is required")
               .NotNull().WithMessage("{PropertyName} is required");

            RuleFor(request => request.UserName).Length(4, 255).WithMessage("The {PropertyName} has {TotalLength} characters. Must be between {MinLength} and {MaxLength} characters.")
               .NotEmpty().WithMessage("{PropertyName} is required")
               .NotNull().WithMessage("{PropertyName} is required");

            RuleFor(request => request.Email)
              .NotEmpty().WithMessage("{PropertyName} is required")
              .NotNull().WithMessage("{PropertyName} is required")
              .EmailAddress().WithMessage(x => "{PropertyName} is not correct");

            RuleFor(request => request.Password).ErpPassword();

            RuleFor(request => request.ConfirmPassword)
               .NotEmpty().WithMessage("{PropertyName} is required")
               .NotNull().WithMessage("{PropertyName} is required")
               .Equal(request => request.Password).WithMessage(x => "Passwords don't match");
        }
    }
}