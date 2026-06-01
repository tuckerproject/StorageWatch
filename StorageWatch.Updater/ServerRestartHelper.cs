using System.Diagnostics;

namespace StorageWatch.Updater;

internal class ServerRestartHelper
{
    private readonly IProcessLauncher _processLauncher;
    private readonly Action<string>? _diagnosticLogger;

    public ServerRestartHelper()
    {
        _processLauncher = new ProcessLauncher();
        _diagnosticLogger = null;
    }

    public ServerRestartHelper(Action<string>? diagnosticLogger)
    {
        _processLauncher = new ProcessLauncher();
        _diagnosticLogger = diagnosticLogger;
    }

    internal ServerRestartHelper(IProcessLauncher processLauncher, Action<string>? diagnosticLogger = null)
    {
        _processLauncher = processLauncher;
        _diagnosticLogger = diagnosticLogger;
    }

    private void LogDiag(string message)
    {
        _diagnosticLogger?.Invoke($"[DIAG] {message}");
    }

    public bool TryRestartServer(string serverLaunchTarget)
    {
        LogDiag($"Restart requested. Component=server, Path={serverLaunchTarget}");
        LogDiag($"File exists check: {serverLaunchTarget} Exists={(!string.IsNullOrWhiteSpace(serverLaunchTarget) && File.Exists(serverLaunchTarget))}");
        if (string.IsNullOrWhiteSpace(serverLaunchTarget))
        {
            Console.WriteLine("Server restart skipped.");
            LogDiag("Restart skipped because server launch target is empty.");
            return false;
        }

        try
        {
            Console.WriteLine("Server restart begins.");
            LogDiag($"Launching restart process: {serverLaunchTarget}");
            var started = _processLauncher.Start(new ProcessStartInfo
            {
                FileName = serverLaunchTarget,
                UseShellExecute = false
            });

            if (!started)
            {
                Console.WriteLine("Server restart failed.");
                LogDiag("Restart launch failed for server.");
                return false;
            }

            Console.WriteLine("Server restart started.");
            LogDiag("Restart launch succeeded for server.");
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Server restart failed: {ex.Message}");
            LogDiag($"Restart launch threw exception for server: {ex.GetType().Name}: {ex.Message}");
            return false;
        }
    }
}
