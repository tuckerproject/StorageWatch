namespace StorageWatchServer.Server.Models;

public class SettingRecord
{
    public required string Key { get; init; }

    public required string Value { get; init; }

    public required string Description { get; init; }
}
