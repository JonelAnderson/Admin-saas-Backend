using DS.Platform.Domain.Enums;

namespace DS.Platform.Application.DTOs.Plans.Request
{
    /// <summary>Formulario de un plan. Los límites del otro producto deben venir vacíos (lo revisa el validador).</summary>
    public class PlanRequestDto
    {
        public required PlanProduct Product { get; set; }
        public required string Code { get; set; }
        public required string Name { get; set; }
        public string? Description { get; set; }
        public decimal MonthlyPrice { get; set; }
        public decimal? YearlyPrice { get; set; }
        public string Currency { get; set; } = "PEN";
        public int TrialDays { get; set; }
        public int SortOrder { get; set; }

        // ERP (null = sin límite)
        public int? MaxCompanies { get; set; }
        public int? MaxUsers { get; set; }
        public int? MaxBranches { get; set; }
        public bool IncludesAi { get; set; }

        // API SUNAT (null = sin límite). En un plan del ERP solo con API incluida (ApiPlanSlug)
        /// <summary>Plan de la API SUNAT que se aplica a las empresas del cliente: free, pro o business.</summary>
        public string? ApiPlanSlug { get; set; }
        public int? MaxIssuers { get; set; }
        public int? MaxDocumentsPerMonth { get; set; }
    }
}
