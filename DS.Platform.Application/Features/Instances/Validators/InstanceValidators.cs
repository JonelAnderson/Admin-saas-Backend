using DS.Platform.Application.Features.Instances.Commands.Create;
using DS.Platform.Application.Features.Instances.Commands.Update;
using DS.Platform.Application.Features.Licensing.Commands.Check;
using FluentValidation;

namespace DS.Platform.Application.Features.Instances.Validators
{
    internal static class InstanceRules
    {
        /// <summary>Vacía o dirección http(s) absoluta.</summary>
        public static bool IsHttpUrl(string? url)
            => string.IsNullOrWhiteSpace(url)
               || (Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps));
    }

    public class CreateInstanceCommandValidator : AbstractValidator<CreateInstanceCommand>
    {
        public CreateInstanceCommandValidator()
        {
            RuleFor(x => x.CreateInstanceRequestDto).NotNull();
            When(x => x.CreateInstanceRequestDto is not null, () =>
            {
                RuleFor(x => x.CreateInstanceRequestDto!.CustomerId).GreaterThan(0);
                RuleFor(x => x.CreateInstanceRequestDto!.Name).NotEmpty().MaximumLength(120);
                RuleFor(x => x.CreateInstanceRequestDto!.Url).MaximumLength(300).Must(InstanceRules.IsHttpUrl)
                    .WithMessage("La dirección debe empezar con http:// o https://.");
                RuleFor(x => x.CreateInstanceRequestDto!.Server).MaximumLength(120);
                RuleFor(x => x.CreateInstanceRequestDto!.Notes).MaximumLength(1000);
            });
        }
    }

    public class UpdateInstanceCommandValidator : AbstractValidator<UpdateInstanceCommand>
    {
        public UpdateInstanceCommandValidator()
        {
            RuleFor(x => x.UpdateInstanceRequestDto).NotNull();
            When(x => x.UpdateInstanceRequestDto is not null, () =>
            {
                RuleFor(x => x.UpdateInstanceRequestDto!.Id).GreaterThan(0);
                RuleFor(x => x.UpdateInstanceRequestDto!.Name).NotEmpty().MaximumLength(120);
                RuleFor(x => x.UpdateInstanceRequestDto!.Url).MaximumLength(300).Must(InstanceRules.IsHttpUrl)
                    .WithMessage("La dirección debe empezar con http:// o https://.");
                RuleFor(x => x.UpdateInstanceRequestDto!.Server).MaximumLength(120);
                RuleFor(x => x.UpdateInstanceRequestDto!.Notes).MaximumLength(1000);
            });
        }
    }

    public class CheckLicenseCommandValidator : AbstractValidator<CheckLicenseCommand>
    {
        public CheckLicenseCommandValidator()
        {
            RuleFor(x => x.LicenseCheckRequestDto).NotNull();
            When(x => x.LicenseCheckRequestDto is not null, () =>
            {
                // El nonce evita reutilizar una respuesta anterior: la instancia manda uno nuevo en cada consulta.
                RuleFor(x => x.LicenseCheckRequestDto!.Nonce).NotEmpty().Length(16, 64).Matches("^[A-Za-z0-9_-]+$");
                RuleFor(x => x.LicenseCheckRequestDto!.Version).MaximumLength(40);
                RuleFor(x => x.LicenseCheckRequestDto!.LastMigration).MaximumLength(150);
            });
        }
    }
}
