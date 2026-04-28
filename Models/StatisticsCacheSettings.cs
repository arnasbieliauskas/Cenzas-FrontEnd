namespace CenzasBackend.Models
{
    public class StatisticsCacheSettings
    {
        public int UpdateIntervalHours { get; set; } = 6;
        public string CacheFilePath { get; set; } = "wwwroot/data/filters-metadata.json";
    }
}
