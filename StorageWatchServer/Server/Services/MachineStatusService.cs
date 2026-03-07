using StorageWatchServer.Services.Logging;

namespace StorageWatchServer.Server.Services;

public class MachineStatusService
{
    private readonly ServerOptions _options;
    private readonly RollingFileLogger? _logger;

    public MachineStatusService(ServerOptions options, RollingFileLogger? logger = null)
    {
        _options = options;
        _logger = logger;
    }

    public bool IsOnline(string machineName, DateTime lastSeenUtc)
    {
        var threshold = GetOnlineThresholdUtc();
        bool isOnline = lastSeenUtc >= threshold;

        _logger?.Log($"[MACHINE STATUS] LastSeenUtc for {machineName} = {lastSeenUtc:yyyy-MM-dd HH:mm:ss}");

        return isOnline;
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
