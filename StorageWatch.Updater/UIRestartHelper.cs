using System.Diagnostics;

namespace StorageWatch.Updater;

internal class UIRestartHelper
{
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
            var process = Process.Start(uiExecutablePath);

            if (process == null)
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
