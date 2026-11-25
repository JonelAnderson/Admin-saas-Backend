namespace DS.Platform.Application.DTOs.Customers.Response
{
    public class CustomerResponseDto
    {
        public int Id { get; set; }
        public string Ruc { get; set; } = null!;
        public string BusinessName { get; set; } = null!;
        public string? TradeName { get; set; }
        public string? Address { get; set; }
        public string? ContactName { get; set; }
        public string? ContactEmail { get; set; }
        public string? ContactPhone { get; set; }
        public string? Notes { get; set; }
        public bool IsActive { get; set; }
        /// <summary>Suscripciones no canceladas (ERP y / o API SUNAT).</summary>
        public int OpenSubscriptions { get; set; }
    }

    /// <summary>Fila del buscador de clientes (combos con muchos registros).</summary>
    public class CustomerLookupResponseDto
    {
        public int Id { get; set; }
        public string Ruc { get; set; } = null!;
        public string BusinessName { get; set; } = null!;
        public string? TradeName { get; set; }
    }
}
