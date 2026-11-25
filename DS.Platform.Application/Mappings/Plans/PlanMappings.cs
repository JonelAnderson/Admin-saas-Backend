using System.Linq.Expressions;
using DS.Platform.Application.DTOs.Plans.Request;
using DS.Platform.Application.DTOs.Plans.Response;
using DS.Platform.Domain.Entities.Billing;
using DS.Platform.Domain.Enums;

namespace DS.Platform.Application.Mappings.Plans
{
    /// <summary>
    /// Mapeo explícito de planes (sin AutoMapper), módulo de referencia de la consola.
    /// Consultas: proyecciones <see cref="Expression"/> que EF traduce a SQL (solo las columnas del DTO). Commands: el DTO se
    /// copia a la entidad campo por campo; estado y auditoría los pone el handler / el contexto. Los límites que no aplican al
    /// producto se guardan vacíos.
    /// </summary>
    public static class PlanMappings
    {
        public const string ErpName = "ERP";
        public const string ApiSunatName = "API SUNAT";

        public static readonly Expression<Func<Plan, PlanResponseDto>> ToResponse = p => new PlanResponseDto
        {
            Id = p.Id,
            Product = p.Product,
            ProductName = p.Product == PlanProduct.Erp ? ErpName : ApiSunatName,
            Code = p.Code,
            Name = p.Name,
            Description = p.Description,
            MonthlyPrice = p.MonthlyPrice,
            YearlyPrice = p.YearlyPrice,
            Currency = p.Currency,
            TrialDays = p.TrialDays,
            SortOrder = p.SortOrder,
            MaxCompanies = p.MaxCompanies,
            MaxUsers = p.MaxUsers,
            MaxBranches = p.MaxBranches,
            IncludesAi = p.IncludesAi,
            ApiPlanSlug = p.ApiPlanSlug,
            MaxIssuers = p.MaxIssuers,
            MaxDocumentsPerMonth = p.MaxDocumentsPerMonth,
            IsActive = p.IsActive
        };

        public static readonly Expression<Func<Plan, PlanSelectResponseDto>> ToSelect = p => new PlanSelectResponseDto
        {
            Id = p.Id,
            Product = p.Product,
            Code = p.Code,
            Name = p.Name,
            MonthlyPrice = p.MonthlyPrice,
            Currency = p.Currency
        };

        /// <summary>Códigos en mayúsculas y sin espacios alrededor: así se comparan y se guardan.</summary>
        public static string NormalizeCode(string code) => code.Trim().ToUpperInvariant();

        /// <summary>Plan nuevo con los datos del formulario.</summary>
        public static Plan ToEntity(this PlanRequestDto dto)
        {
            var plan = new Plan { Product = dto.Product, Code = NormalizeCode(dto.Code), Name = dto.Name.Trim() };
            CopyEditable(dto, plan);
            return plan;
        }

        /// <summary>Copia los datos editables del formulario al plan (no toca id, estado ni auditoría).</summary>
        public static void ApplyTo(this PlanRequestDto dto, Plan plan)
        {
            plan.Product = dto.Product;
            plan.Code = NormalizeCode(dto.Code);
            plan.Name = dto.Name.Trim();
            CopyEditable(dto, plan);
        }

        private static void CopyEditable(PlanRequestDto dto, Plan plan)
        {
            plan.Description = string.IsNullOrWhiteSpace(dto.Description) ? null : dto.Description.Trim();
            plan.MonthlyPrice = dto.MonthlyPrice;
            plan.YearlyPrice = dto.YearlyPrice;
            plan.Currency = dto.Currency.Trim().ToUpperInvariant();
            plan.TrialDays = dto.TrialDays;
            plan.SortOrder = dto.SortOrder;

            var isErp = dto.Product == PlanProduct.Erp;
            plan.MaxCompanies = isErp ? dto.MaxCompanies : null;
            plan.MaxUsers = isErp ? dto.MaxUsers : null;
            plan.MaxBranches = isErp ? dto.MaxBranches : null;
            plan.IncludesAi = isErp && dto.IncludesAi;
            // La API SUNAT: siempre en un plan de la API; en uno del ERP solo si la lleva incluida (plan de la API indicado).
            plan.ApiPlanSlug = string.IsNullOrWhiteSpace(dto.ApiPlanSlug) ? null : dto.ApiPlanSlug.Trim().ToLowerInvariant();
            var hasApi = !isErp || plan.ApiPlanSlug is not null;
            plan.MaxIssuers = hasApi ? dto.MaxIssuers : null;
            plan.MaxDocumentsPerMonth = hasApi ? dto.MaxDocumentsPerMonth : null;
        }
    }
}
