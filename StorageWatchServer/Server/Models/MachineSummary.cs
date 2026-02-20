namespace StorageWatchServer.Server.Models;

public class MachineSummary
{
    public int Id { get; init; }

    public required string MachineName { get; init; }

    public DateTime LastSeenUtc { get; init; }

    public IReadOnlyList<MachineDriveStatus> Drives { get; set; } = Array.Empty<MachineDriveStatus>();
}
