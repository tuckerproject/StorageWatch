namespace StorageWatchServer.Server.Reporting.Models;

public class AgentReport
{
    public string AgentId { get; set; } = string.Empty;

    public DateTime TimestampUtc { get; set; }

    public List<DriveReport> Drives { get; set; } = new();

    public List<AlertRecord> Alerts { get; set; } = new();
}
