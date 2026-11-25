using DS.Platform.Domain.Contracts;
using DS.Platform.Domain.Enums;

namespace DS.Platform.Domain.Entities.Billing
{
    /// <summary>
    /// Plan comercial de un producto (ERP o API SUNAT). Los límites que no aplican al producto quedan en null, y null en un
    /// límite que sí aplica significa "sin límite". Lo usarán las suscripciones (fase C2): un plan con suscripciones no se borra.
    /// </summary>
    public class Plan : AuditableEntity<int>, IConcurrencyChecked
    {
        public required PlanProduct Product { get; set; }
        /// <summary>Identificador corto (mayúsculas, números, guion), único por producto: ERP-BASICO, API-1000…</summary>
        public required string Code { get; set; }
        public required string Name { get; set; }
        public string? Description { get; set; }

        /// <summary>Precio mensual (sin IGV) en <see cref="Currency"/>.</summary>
        public decimal MonthlyPrice { get; set; }
        /// <summary>Precio anual (sin IGV); null = no se vende anual.</summary>
        public decimal? YearlyPrice { get; set; }
        /// <summary>PEN o USD.</summary>
        public string Currency { get; set; } = "PEN";
        /// <summary>Días de prueba al dar de alta (0 = sin prueba).</summary>
        public int TrialDays { get; set; }
        /// <summary>Orden en los listados y combos.</summary>
        public int SortOrder { get; set; }

        // Límites del ERP
        public int? MaxCompanies { get; set; }
        public int? MaxUsers { get; set; }
        public int? MaxBranches { get; set; }
        public bool IncludesAi { get; set; }

        // API SUNAT: en un plan de la API es el plan que se aplica a sus empresas; en un plan del ERP indica que la API va incluida
        /// <summary>Plan de la API SUNAT que se aplica (free, pro o business). Obligatorio en planes de la API; en uno del ERP, null = sin API incluida.</summary>
        public string? ApiPlanSlug { get; set; }

        // Límites de la API SUNAT (en un plan del ERP solo con API incluida)
        /// <summary>Empresas emisoras (RUC) del cliente.</summary>
        public int? MaxIssuers { get; set; }
        public int? MaxDocumentsPerMonth { get; set; }
    }
}
