using DS.Platform.Domain.Contracts;
using DS.Platform.Domain.Entities.Billing;
using System.Collections.Generic;

namespace DS.Platform.Domain.Entities.Customers
{
    /// <summary>
    /// Cliente de la plataforma: la empresa que contrata el ERP, la API SUNAT o ambos (no confundir con los clientes que cada
    /// empresa registra en Ventas del ERP). Se identifica por su RUC.
    /// </summary>
    public class Customer : AuditableEntity<int>, IConcurrencyChecked
    {
        /// <summary>RUC de 11 dígitos (validado con su dígito verificador), único entre los clientes no borrados.</summary>
        public required string Ruc { get; set; }
        public required string BusinessName { get; set; }
        public string? TradeName { get; set; }
        public string? Address { get; set; }

        // Contacto principal (avisos de cobro y técnicos).
        public string? ContactName { get; set; }
        public string? ContactEmail { get; set; }
        public string? ContactPhone { get; set; }

        public string? Notes { get; set; }

        public virtual ICollection<Subscription> Subscriptions { get; set; } = new List<Subscription>();
    }
}
