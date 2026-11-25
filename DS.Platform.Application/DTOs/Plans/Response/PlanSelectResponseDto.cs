using DS.Platform.Domain.Enums;

namespace DS.Platform.Application.DTOs.Plans.Response
{
    public class PlanSelectResponseDto
    {
        public int Id { get; set; }
        public PlanProduct Product { get; set; }
        public string Code { get; set; } = null!;
        public string Name { get; set; } = null!;
        public decimal MonthlyPrice { get; set; }
        public string Currency { get; set; } = null!;
    }
}
