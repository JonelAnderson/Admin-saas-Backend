using DS.Platform.Domain.Contracts;
using DS.Platform.Domain.Entities.Customers;
using System;

namespace DS.Platform.Domain.Entities.ApiSunat
{
    /// <summary>
    /// Empresa emisora de la API SUNAT (un tenant de esa API) que pertenece a un cliente de la consola. Un RUC solo se
    /// vincula una vez. Los datos de la API (razón social, ambiente, plan, uso) son una copia de la última sincronización;
    /// la fuente de verdad es la API SUNAT.
    /// </summary>
    public class ApiSunatIssuer : AuditableEntity<int>, IConcurrencyChecked
    {
        public required int CustomerId { get; set; }

        /// <summary>Id de la empresa en la API SUNAT (<c>tenants.id</c>).</summary>
        public required long TenantId { get; set; }
        public required string Ruc { get; set; }
        public required string BusinessName { get; set; }
        public string? TradeName { get; set; }

        /// <summary>beta / production, tal como lo informa la API.</summary>
        public string? Environment { get; set; }
        /// <summary>free / pro / business, tal como lo informa la API.</summary>
        public string? ApiPlan { get; set; }
        public int? DocumentsThisMonth { get; set; }
        public int? MaxDocumentsMonth { get; set; }

        /// <summary>Lo que la consola quiere: suspender la emisión (el cliente no tiene ninguna suscripción vigente).</summary>
        public bool SuspendDesired { get; set; }
        /// <summary>Lo que la API confirmó. Si difiere de <see cref="SuspendDesired"/>, hay una acción remota en camino.</summary>
        public bool SuspendApplied { get; set; }

        /// <summary>Plan que la consola quiere para la empresa según las suscripciones del cliente; null = no se administra.</summary>
        public string? PlanDesiredSlug { get; set; }
        /// <summary>Cupo mensual que la consola quiere; null = el del plan.</summary>
        public int? PlanDesiredMaxDocuments { get; set; }

        public DateTime? LastSyncedOnUtc { get; set; }
        public string? Notes { get; set; }

        public virtual Customer? Customer { get; set; }
    }
}
