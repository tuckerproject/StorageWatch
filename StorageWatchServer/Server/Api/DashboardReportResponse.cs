namespace StorageWatchServer.Server.Api;

public sealed class DashboardReportResponse
{
    public string AgentId { get; init; } = string.Empty;

    public DateTime TimestampUtc { get; init; }

    public IReadOnlyList<DashboardDriveSummary> Drives { get; init; } = Array.Empty<DashboardDriveSummary>();

    public IReadOnlyList<DashboardAlertSummary> Alerts { get; init; } = Array.Empty<DashboardAlertSummary>();
}

public sealed class DashboardDriveSummary
{
    public string DriveLetter { get; init; } = string.Empty;

    public double UsedPercent { get; init; }
}

public sealed class DashboardAlertSummary
{
    public string DriveLetter { get; init; } = string.Empty;

    public string Level { get; init; } = string.Empty;

    public string Message { get; init; } = string.Empty;
}
