namespace StorageWatchServer.Server.Models;

public class AlertRecord
{
    public int Id { get; init; }

    public int MachineId { get; init; }

    public required string MachineName { get; init; }

    public required string Severity { get; init; }

    public required string Message { get; init; }

    public DateTime CreatedUtc { get; init; }

    public DateTime? ResolvedUtc { get; init; }

    public bool IsActive { get; init; }
}
