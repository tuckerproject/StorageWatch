using System.Diagnostics;

namespace StorageWatch.Updater;

internal class ServerRestartHelper
{
    private readonly IProcessLauncher _processLauncher;

    public ServerRestartHelper()
    {
        _processLauncher = new ProcessLauncher();
    }

    internal ServerRestartHelper(IProcessLauncher processLauncher)
    {
        _processLauncher = processLauncher;
    }

    public bool TryRestartServer(string serverLaunchTarget)
    {
        if (string.IsNullOrWhiteSpace(serverLaunchTarget))
        {
            Console.WriteLine("Server restart skipped.");
            return false;
        }

        try
        {
            Console.WriteLine("Server restart begins.");
            var started = _processLauncher.Start(new ProcessStartInfo
            {
                FileName = serverLaunchTarget,
                UseShellExecute = false
            });

            if (!started)
            {
                Console.WriteLine("Server restart failed.");
                return false;
            }

            Console.WriteLine("Server restart started.");
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Server restart failed: {ex.Message}");
            return false;
        }
    }
}
