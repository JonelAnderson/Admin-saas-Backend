namespace DS.Platform.Application.DTOs.Customers.Request
{
    public class CustomerRequestDto
    {
        public required string Ruc { get; set; }
        public required string BusinessName { get; set; }
        public string? TradeName { get; set; }
        public string? Address { get; set; }
        public string? ContactName { get; set; }
        public string? ContactEmail { get; set; }
        public string? ContactPhone { get; set; }
        public string? Notes { get; set; }
    }

    public class UpdateCustomerRequestDto : CustomerRequestDto
    {
        public required int Id { get; set; }
    }
}
