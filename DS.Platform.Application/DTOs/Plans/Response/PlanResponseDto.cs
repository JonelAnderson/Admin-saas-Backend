using DS.Platform.Domain.Enums;

namespace DS.Platform.Application.DTOs.Plans.Response
{
    public class PlanResponseDto
    {
        public int Id { get; set; }
        public PlanProduct Product { get; set; }
        /// <summary>"ERP" o "API SUNAT", para mostrar.</summary>
        public string ProductName { get; set; } = null!;
        public string Code { get; set; } = null!;
        public string Name { get; set; } = null!;
        public string? Description { get; set; }
        public decimal MonthlyPrice { get; set; }
        public decimal? YearlyPrice { get; set; }
        public string Currency { get; set; } = null!;
        public int TrialDays { get; set; }
        public int SortOrder { get; set; }
        public int? MaxCompanies { get; set; }
        public int? MaxUsers { get; set; }
        public int? MaxBranches { get; set; }
        public bool IncludesAi { get; set; }
        public string? ApiPlanSlug { get; set; }
        public int? MaxIssuers { get; set; }
        public int? MaxDocumentsPerMonth { get; set; }
        public bool IsActive { get; set; }
    }
}
