namespace StorageWatchServer.Server.Api;

/// <summary>
/// Represents the request body for POST /api/agent/report.
/// Sent exactly as the Agent provides it, with no transformation.
/// </summary>
public class AgentReportRequest
{
    public string MachineName { get; set; } = string.Empty;

    public List<RawDriveRowRequest> Rows { get; set; } = new();
}

/// <summary>
/// Represents a single raw drive row in the agent report request.
/// </summary>
public class RawDriveRowRequest
{
    public string DriveLetter { get; set; } = string.Empty;

    public double TotalSpaceGb { get; set; }

    public double UsedSpaceGb { get; set; }

    public double FreeSpaceGb { get; set; }

    public double PercentFree { get; set; }

    public DateTime Timestamp { get; set; }
}
