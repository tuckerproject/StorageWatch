namespace StorageWatch.Models;

public class AgentReportRequest
{
    public string AgentId { get; set; } = string.Empty;

    public DateTime TimestampUtc { get; set; }

    public List<DriveReportDto> Drives { get; set; } = new();

    public List<AlertDto> Alerts { get; set; } = new();
}
