using System;
using System.Collections.Generic;

namespace CenzasBackend.Models
{
    public class FilterCombination
    {
        public required string District { get; set; }
        public string? Street { get; set; }
        public int Rooms { get; set; }
        public required string Title { get; set; }
        public string? Heating { get; set; }
        public string? Equipped { get; set; }
        public string? EnergyClass { get; set; }
        public string? CollectedOn { get; set; }
        public double Price { get; set; }
        public double Area { get; set; }
    }

    public class CityData
    {
        public required string City { get; set; }
        public List<string> Streets { get; set; } = new List<string>();
        public List<FilterCombination> Combinations { get; set; } = new List<FilterCombination>();
        public double AverageRentPriceSqm { get; set; }
        public double AverageSalePriceSqm { get; set; }
    }

    public class FilterMetadata
    {
        public DateTime LastUpdated { get; set; }
        public List<CityData> Cities { get; set; } = new List<CityData>();
        public DataSanitizationSettings? DataSanitizationSettings { get; set; }
    }
}
