using System.Diagnostics;

namespace StorageWatch.Updater;

internal class UIRestartHelper
{
    private readonly IProcessLauncher _processLauncher;
    private readonly Action<string>? _diagnosticLogger;

    public UIRestartHelper()
    {
        _processLauncher = new ProcessLauncher();
        _diagnosticLogger = null;
    }

    public UIRestartHelper(Action<string>? diagnosticLogger)
    {
        _processLauncher = new ProcessLauncher();
        _diagnosticLogger = diagnosticLogger;
    }

    internal UIRestartHelper(IProcessLauncher processLauncher, Action<string>? diagnosticLogger = null)
    {
        _processLauncher = processLauncher;
        _diagnosticLogger = diagnosticLogger;
    }

    private void LogDiag(string message)
    {
        _diagnosticLogger?.Invoke($"[DIAG] {message}");
    }

    public bool TryRestartUI(string uiExecutablePath)
    {
        LogDiag($"Restart requested. Component=ui, Path={uiExecutablePath}");
        LogDiag($"File exists check: {uiExecutablePath} Exists={(!string.IsNullOrWhiteSpace(uiExecutablePath) && File.Exists(uiExecutablePath))}");
        if (string.IsNullOrWhiteSpace(uiExecutablePath))
        {
            Console.WriteLine("UI relaunch skipped.");
            LogDiag("Restart skipped because ui executable path is empty.");
            return false;
        }

        try
        {
            Console.WriteLine("UI relaunch begins.");
            LogDiag($"Launching restart process: {uiExecutablePath}");
            var started = _processLauncher.Start(new ProcessStartInfo
            {
                FileName = uiExecutablePath,
                UseShellExecute = false
            });

            if (!started)
            {
                Console.WriteLine("UI relaunch failed.");
                LogDiag("Restart launch failed for UI.");
                return false;
            }

            LogDiag("Restart launch succeeded for UI.");
            return true;
        }
        catch
        {
            Console.WriteLine("UI relaunch failed.");
            LogDiag("Restart launch threw exception for UI.");
            return false;
        }
    }
}
