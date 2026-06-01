using System.ServiceProcess;

namespace StorageWatch.Updater;

internal class AgentRestartHelper
{
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromMinutes(2);
    private readonly Action<string>? _diagnosticLogger;

    public AgentRestartHelper(Action<string>? diagnosticLogger = null)
    {
        _diagnosticLogger = diagnosticLogger;
    }

    private void LogDiag(string message)
    {
        _diagnosticLogger?.Invoke($"[DIAG] {message}");
    }

    public bool TryRestartAgentService(string serviceName)
    {
        LogDiag($"Restart requested. Component=agent, ServiceName={serviceName}");
        if (!OperatingSystem.IsWindows())
        {
            Console.WriteLine("Agent service restart skipped.");
            LogDiag("Service restart skipped because current OS is not Windows.");
            return false;
        }

        if (string.IsNullOrWhiteSpace(serviceName))
        {
            Console.WriteLine("Agent service restart skipped.");
            LogDiag("Service restart skipped because service name is empty.");
            return false;
        }

        try
        {
            Console.WriteLine("Agent service restart begins.");

            using var serviceController = new ServiceController(serviceName);
            var statusBefore = serviceController.Status;
            LogDiag($"Service restart: {serviceName}, StatusBefore={statusBefore}");

            if (serviceController.Status != ServiceControllerStatus.Stopped &&
                serviceController.Status != ServiceControllerStatus.StopPending)
            {
                serviceController.Stop();
                serviceController.WaitForStatus(ServiceControllerStatus.Stopped, DefaultTimeout);
                LogDiag($"Service restart: {serviceName}, Transition=Stopped");
            }

            serviceController.Start();
            serviceController.WaitForStatus(ServiceControllerStatus.Running, DefaultTimeout);
            var statusAfter = serviceController.Status;
            LogDiag($"Service restart: {serviceName}, StatusAfter={statusAfter}");

            Console.WriteLine("Agent service restart completed.");
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Agent service restart failed: {ex.Message}");
            LogDiag($"Service restart failed: {serviceName}, Error={ex.GetType().Name}: {ex.Message}");
            return false;
        }
    }
}
