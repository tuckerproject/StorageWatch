namespace StorageWatchServer.Server.Reporting.Models;

public class AlertRecord
{
    public string DriveLetter { get; set; } = string.Empty;

    public string Level { get; set; } = string.Empty;

    public string Message { get; set; } = string.Empty;

    public DateTime TimestampUtc { get; set; }
}
