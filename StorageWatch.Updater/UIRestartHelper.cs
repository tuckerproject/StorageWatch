using System.Diagnostics;

namespace StorageWatch.Updater;

internal class UIRestartHelper
{
    private readonly IProcessLauncher _processLauncher;

    public UIRestartHelper()
    {
        _processLauncher = new ProcessLauncher();
    }

    internal UIRestartHelper(IProcessLauncher processLauncher)
    {
        _processLauncher = processLauncher;
    }

    public bool TryRestartUI(string uiExecutablePath)
    {
        if (string.IsNullOrWhiteSpace(uiExecutablePath))
        {
            Console.WriteLine("UI relaunch skipped.");
            return false;
        }

        try
        {
            Console.WriteLine("UI relaunch begins.");
            var started = _processLauncher.Start(new ProcessStartInfo
            {
                FileName = uiExecutablePath,
                UseShellExecute = false
            });

            if (!started)
            {
                Console.WriteLine("UI relaunch failed.");
                return false;
            }

            return true;
        }
        catch
        {
            Console.WriteLine("UI relaunch failed.");
            return false;
        }
    }
}
