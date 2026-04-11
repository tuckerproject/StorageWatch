using System.ServiceProcess;

namespace StorageWatch.Updater;

internal class AgentRestartHelper
{
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromMinutes(2);

    public bool TryRestartAgentService(string serviceName)
    {
        if (!OperatingSystem.IsWindows())
        {
            Console.WriteLine("Agent service restart skipped.");
            return false;
        }

        if (string.IsNullOrWhiteSpace(serviceName))
        {
            Console.WriteLine("Agent service restart skipped.");
            return false;
        }

        try
        {
            Console.WriteLine("Agent service restart begins.");

            using var serviceController = new ServiceController(serviceName);

            if (serviceController.Status != ServiceControllerStatus.Stopped &&
                serviceController.Status != ServiceControllerStatus.StopPending)
            {
                serviceController.Stop();
                serviceController.WaitForStatus(ServiceControllerStatus.Stopped, DefaultTimeout);
            }

            serviceController.Start();
            serviceController.WaitForStatus(ServiceControllerStatus.Running, DefaultTimeout);

            Console.WriteLine("Agent service restart completed.");
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Agent service restart failed: {ex.Message}");
            return false;
        }
    }
}
