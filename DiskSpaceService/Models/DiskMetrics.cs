namespace DiskSpaceService.Models
{
    public class DiskMetrics
    {
        public string MachineName { get; set; }
        public string DriveLetter { get; set; }
        public decimal TotalSpaceGB { get; set; }
        public decimal UsedSpaceGB { get; set; }
        public decimal FreeSpaceGB { get; set; }
        public decimal PercentFree { get; set; }
    }
}