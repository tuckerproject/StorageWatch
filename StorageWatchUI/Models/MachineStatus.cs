namespace StorageWatchUI.Models;

/// <summary>
/// Represents the status of a machine reporting to the central server.
/// </summary>
public class MachineStatus
{
    public string MachineName { get; set; } = string.Empty;
    public DateTime LastReportTime { get; set; }
    public bool IsOnline => (DateTime.UtcNow - LastReportTime).TotalMinutes < 10;
    public List<DiskInfo> Disks { get; set; } = new();
    public DiskStatusLevel WorstStatus => Disks.Any() 
        ? Disks.Max(d => d.Status) 
        : DiskStatusLevel.OK;
}
