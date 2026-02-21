namespace StorageWatch.Models;

public class AgentReportRequest
{
    public string MachineName { get; set; } = string.Empty;

    public DateTime CollectionTimeUtc { get; set; }

    public List<AgentDriveReport> Drives { get; set; } = new();
}

public class AgentDriveReport
{
    public string DriveLetter { get; set; } = string.Empty;

    public double TotalSpaceGb { get; set; }

    public double UsedSpaceGb { get; set; }

    public double FreeSpaceGb { get; set; }

    public double PercentFree { get; set; }

    public DateTime CollectionTimeUtc { get; set; }
}
