using System.Diagnostics;

namespace StorageWatch.Updater;

internal class ServerRestartHelper
{
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
            var process = Process.Start(serverLaunchTarget);

            if (process == null)
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
