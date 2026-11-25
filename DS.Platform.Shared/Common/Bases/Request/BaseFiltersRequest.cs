using DS.Platform.Domain.Enums;

namespace DS.Platform.Shared.Commons.Bases.Request
{
    public class BaseFiltersRequest : BasePaginationRequest
    {
        public string? TextFilter { get; set; } = null;
        public string? PriceTypeFilter { get; set; } = null;
        public int? StateFilter { get; set; } = null;
        public string? StartDate { get; set; } = null;
        public string? EndDate { get; set; } = null;
        public int? LastId { get; set; } = null;
        public string? LastStringValue { get; set; } = null;
        public bool? Download { get; set; } = false;
    }
}