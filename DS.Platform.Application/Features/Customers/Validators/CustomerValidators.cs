using DS.Platform.Application.DTOs.Customers.Request;
using DS.Platform.Application.Features.Customers.Commands.ChangeState;
using DS.Platform.Application.Features.Customers.Commands.Create;
using DS.Platform.Application.Features.Customers.Commands.Delete;
using DS.Platform.Application.Features.Customers.Commands.Update;
using FluentValidation;

namespace DS.Platform.Application.Features.Customers.Validators
{
    public class CustomerRequestValidator : AbstractValidator<CustomerRequestDto>
    {
        public CustomerRequestValidator()
        {
            RuleFor(x => x.Ruc).Must(RucRules.IsValid).WithMessage(RucRules.InvalidMessage);
            RuleFor(x => x.BusinessName).NotEmpty().MaximumLength(200);
            RuleFor(x => x.TradeName).MaximumLength(200);
            RuleFor(x => x.Address).MaximumLength(300);
            RuleFor(x => x.ContactName).MaximumLength(150);
            RuleFor(x => x.ContactEmail).MaximumLength(150).EmailAddress().When(x => !string.IsNullOrWhiteSpace(x.ContactEmail));
            RuleFor(x => x.ContactPhone).MaximumLength(20)
                .Matches(@"^\+?[0-9 ]{6,20}$").WithMessage("El teléfono solo admite números, espacios y un + inicial.")
                .When(x => !string.IsNullOrWhiteSpace(x.ContactPhone));
            RuleFor(x => x.Notes).MaximumLength(1000);
        }
    }

    public class CreateCustomerCommandValidator : AbstractValidator<CreateCustomerCommand>
    {
        public CreateCustomerCommandValidator()
        {
            RuleFor(x => x.CustomerRequestDto).NotNull().SetValidator(new CustomerRequestValidator()!);
        }
    }

    public class UpdateCustomerCommandValidator : AbstractValidator<UpdateCustomerCommand>
    {
        public UpdateCustomerCommandValidator()
        {
            RuleFor(x => x.UpdateCustomerRequestDto).NotNull().SetValidator(new CustomerRequestValidator()!);
            RuleFor(x => x.UpdateCustomerRequestDto!.Id).GreaterThan(0).When(x => x.UpdateCustomerRequestDto is not null);
        }
    }

    public class ChangeStateCustomerCommandValidator : AbstractValidator<ChangeStateCustomerCommand>
    {
        public ChangeStateCustomerCommandValidator() => RuleFor(x => x.Id).GreaterThan(0);
    }

    public class DeleteCustomerCommandValidator : AbstractValidator<DeleteCustomerCommand>
    {
        public DeleteCustomerCommandValidator() => RuleFor(x => x.Id).GreaterThan(0);
    }
}
