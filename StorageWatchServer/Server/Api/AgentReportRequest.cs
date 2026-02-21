namespace StorageWatchServer.Server.Api;

public class AgentReportRequest
{
    public string AgentId { get; set; } = string.Empty;

    public DateTime TimestampUtc { get; set; }

    public List<DriveReportDto> Drives { get; set; } = new();

    public List<AlertDto> Alerts { get; set; } = new();
}

public class DriveReportDto
{
    public string DriveLetter { get; set; } = string.Empty;

    public double TotalSpaceGb { get; set; }

    public double FreeSpaceGb { get; set; }

    public double UsedPercent { get; set; }
}

public class AlertDto
{
    public string DriveLetter { get; set; } = string.Empty;

    public string Level { get; set; } = string.Empty;

    public string Message { get; set; } = string.Empty;
}
