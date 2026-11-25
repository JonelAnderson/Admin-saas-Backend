using DS.Platform.Application.DTOs.Plans.Request;
using DS.Platform.Application.Features.Plans.Commands.ChangeState;
using DS.Platform.Application.Features.Plans.Commands.Create;
using DS.Platform.Application.Features.Plans.Commands.Delete;
using DS.Platform.Application.Features.Plans.Commands.Update;
using DS.Platform.Domain.Enums;
using FluentValidation;

namespace DS.Platform.Application.Features.Plans.Validators
{
    /// <summary>Datos del formulario de un plan (crear y editar).</summary>
    public class PlanRequestValidator : AbstractValidator<PlanRequestDto>
    {
        public static readonly string[] Currencies = ["PEN", "USD"];
        public const int MaxTrialDays = 90;
        /// <summary>Planes de la API SUNAT (los slugs que acepta <c>PUT tenants/{id}/plan</c>).</summary>
        public static readonly string[] ApiPlanSlugs = ["free", "pro", "business"];

        public PlanRequestValidator()
        {
            RuleFor(x => x.Product).IsInEnum().WithMessage("Seleccione el producto del plan (ERP o API SUNAT).");
            RuleFor(x => x.Code).NotEmpty().MaximumLength(32)
                .Matches("^[A-Za-z0-9_-]+$").WithMessage("El código solo admite letras, números, guion y guion bajo.");
            RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
            RuleFor(x => x.Description).MaximumLength(500);
            RuleFor(x => x.MonthlyPrice).GreaterThanOrEqualTo(0).PrecisionScale(12, 2, true);
            RuleFor(x => x.YearlyPrice).GreaterThanOrEqualTo(0).PrecisionScale(12, 2, true).When(x => x.YearlyPrice.HasValue);
            RuleFor(x => x.Currency).Must(c => c is not null && Currencies.Contains(c.Trim().ToUpperInvariant()))
                .WithMessage("La moneda debe ser PEN o USD.");
            RuleFor(x => x.TrialDays).InclusiveBetween(0, MaxTrialDays);
            RuleFor(x => x.SortOrder).GreaterThanOrEqualTo(0);

            // Límites: null = sin límite; si se indica, al menos 1.
            RuleFor(x => x.MaxCompanies).GreaterThanOrEqualTo(1).When(x => x.MaxCompanies.HasValue);
            RuleFor(x => x.MaxUsers).GreaterThanOrEqualTo(1).When(x => x.MaxUsers.HasValue);
            RuleFor(x => x.MaxBranches).GreaterThanOrEqualTo(1).When(x => x.MaxBranches.HasValue);
            RuleFor(x => x.MaxIssuers).GreaterThanOrEqualTo(1).When(x => x.MaxIssuers.HasValue);
            RuleFor(x => x.MaxDocumentsPerMonth).GreaterThanOrEqualTo(1).When(x => x.MaxDocumentsPerMonth.HasValue);

            RuleFor(x => x.ApiPlanSlug).Must(s => string.IsNullOrWhiteSpace(s) || ApiPlanSlugs.Contains(s.Trim().ToLowerInvariant()))
                .WithMessage("El plan de la API SUNAT debe ser free, pro o business.");

            // Los límites del otro producto deben venir vacíos: así el formulario no guarda datos que no se aplican.
            // Un plan del ERP lleva límites de la API solo si la incluye (indica su plan de la API).
            When(x => x.Product == PlanProduct.Erp, () =>
            {
                RuleFor(x => x.MaxIssuers).Null().When(x => string.IsNullOrWhiteSpace(x.ApiPlanSlug))
                    .WithMessage("Un plan del ERP sin API SUNAT incluida no lleva límite de empresas emisoras.");
                RuleFor(x => x.MaxDocumentsPerMonth).Null().When(x => string.IsNullOrWhiteSpace(x.ApiPlanSlug))
                    .WithMessage("Un plan del ERP sin API SUNAT incluida no lleva límite de comprobantes por mes.");
            });
            When(x => x.Product == PlanProduct.ApiSunat, () =>
            {
                RuleFor(x => x.ApiPlanSlug).NotEmpty().WithMessage("Indique el plan de la API SUNAT que se aplica (free, pro o business).");
                RuleFor(x => x.MaxCompanies).Null().WithMessage("Un plan de la API SUNAT no lleva límite de empresas del ERP.");
                RuleFor(x => x.MaxUsers).Null().WithMessage("Un plan de la API SUNAT no lleva límite de usuarios.");
                RuleFor(x => x.MaxBranches).Null().WithMessage("Un plan de la API SUNAT no lleva límite de sucursales.");
                RuleFor(x => x.IncludesAi).Equal(false).WithMessage("La IA solo se incluye en planes del ERP.");
            });
        }
    }

    public class CreatePlanCommandValidator : AbstractValidator<CreatePlanCommand>
    {
        public CreatePlanCommandValidator()
        {
            RuleFor(x => x.PlanRequestDto).NotNull().SetValidator(new PlanRequestValidator()!);
        }
    }

    public class UpdatePlanCommandValidator : AbstractValidator<UpdatePlanCommand>
    {
        public UpdatePlanCommandValidator()
        {
            RuleFor(x => x.UpdatePlanRequestDto).NotNull().SetValidator(new PlanRequestValidator()!);
            RuleFor(x => x.UpdatePlanRequestDto!.Id).GreaterThan(0).When(x => x.UpdatePlanRequestDto is not null);
        }
    }

    public class ChangeStatePlanCommandValidator : AbstractValidator<ChangeStatePlanCommand>
    {
        public ChangeStatePlanCommandValidator()
        {
            RuleFor(x => x.Id).GreaterThan(0);
        }
    }

    public class DeletePlanCommandValidator : AbstractValidator<DeletePlanCommand>
    {
        public DeletePlanCommandValidator()
        {
            RuleFor(x => x.Id).GreaterThan(0);
        }
    }
}
