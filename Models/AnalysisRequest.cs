namespace CenzasBackend.Models
{
    public class AnalysisRequest
    {
        public string? City { get; set; }
        public List<string>? Districts { get; set; }
        public List<string>? Streets { get; set; }
        public List<int>? Rooms { get; set; }
        public List<string>? Objects { get; set; }
        public string? BuildingType { get; set; }
        public decimal? AreaFrom { get; set; }
        public decimal? AreaTo { get; set; }
        public decimal? PriceFrom { get; set; }
        public decimal? PriceTo { get; set; }
        public string? DateFrom { get; set; }
        public string? DateTo { get; set; }
        public string? PriceStatus { get; set; }
        public string? ValidityStatus { get; set; }
        public int? ExpiredThresholdDays { get; set; }
        public string? SortBy { get; set; }

        // Rule #12 / Rule #15: Extended Building Filters (multi-select as arrays)
        public int? BuildYearFrom { get; set; }
        public int? BuildYearTo { get; set; }
        public int? RenovationYearFrom { get; set; }
        public int? RenovationYearTo { get; set; }
        public List<string>? Heating { get; set; }
        public List<string>? Equipped { get; set; }
        public List<string>? EnergyClass { get; set; }

        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 20;
    }
}
