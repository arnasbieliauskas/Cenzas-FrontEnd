namespace CenzasBackend.Models
{
    public class DataSanitizationSettings
    {
        public int RentAnomalyThresholdPct { get; set; } = 1000;
        public int SaleAnomalyThresholdPct { get; set; } = 500;
        public int LuxuryBufferPct { get; set; } = 150;
        public int HardMaxRentEurSqm { get; set; } = 120;
        public int HardMinSaleEurSqm { get; set; } = 300;
    }
}
