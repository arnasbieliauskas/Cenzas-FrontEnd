using System;

namespace CenzasBackend.Models
{
    public class Listing
    {
        public string? Title { get; set; }
        public string? Address { get; set; }
        public int? Rooms { get; set; }
        public double? Area { get; set; }
        public string? Url { get; set; }
        public string? City { get; set; }
        public string? District { get; set; }
        public decimal? Price { get; set; }
        public decimal? PricePerSqM { get; set; }
        
        // Extended Lifecycle Dates
        public string? FirstCollectedDate { get; set; }
        public string? LastCollectedDate { get; set; }

        public decimal? LastPrice { get; set; }
        public decimal? LastPricePerSqM { get; set; }

        // House Details / Extra metadata from h.*
        public string? BuildYear { get; set; }
        public string? HouseType { get; set; }
        public double? LotSize { get; set; }
        public string? Heating { get; set; }
        public string? Equipment { get; set; }
    }
}
