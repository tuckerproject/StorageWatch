namespace StorageWatchServer.Server.Services;

public class MachineStatusService
{
    private readonly ServerOptions _options;

    public MachineStatusService(ServerOptions options)
    {
        _options = options;
    }

    public bool IsOnline(DateTime lastSeenUtc)
    {
        return lastSeenUtc >= GetOnlineThresholdUtc();
    }

    public DateTime GetOnlineThresholdUtc()
    {
        return DateTime.UtcNow.AddMinutes(-_options.OnlineTimeoutMinutes);
    }
}
