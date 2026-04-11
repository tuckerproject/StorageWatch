using StorageWatch.Services.Logging;
using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

namespace StorageWatch.Services.AutoUpdate
{
    public interface IServiceRestartHandler
    {
        void RequestRestart();
    }

    public class UpdaterServiceRestartHandler : IServiceRestartHandler
    {
        private const string DefaultServiceName = "StorageWatchAgent";
        private readonly RollingFileLogger _logger;
        private readonly string _serviceName;
        private readonly Func<ProcessStartInfo, Process?> _processStarter;
        private readonly Action _exitAction;

        public UpdaterServiceRestartHandler(RollingFileLogger logger)
            : this(
                logger,
                Environment.GetEnvironmentVariable("STORAGEWATCH_AGENT_SERVICE_NAME"),
                processStartInfo => Process.Start(processStartInfo),
                () => Environment.Exit(0))
        {
        }

        public UpdaterServiceRestartHandler(
            RollingFileLogger logger,
            string? serviceName,
            Func<ProcessStartInfo, Process?> processStarter,
            Action exitAction)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _serviceName = string.IsNullOrWhiteSpace(serviceName) ? DefaultServiceName : serviceName.Trim();
            _processStarter = processStarter ?? throw new ArgumentNullException(nameof(processStarter));
            _exitAction = exitAction ?? throw new ArgumentNullException(nameof(exitAction));
        }

        public void RequestRestart()
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                _logger.Log("[AUTOUPDATE] Restart requested on non-Windows OS. Restart skipped.");
                return;
            }

            try
            {
                var updaterPath = ResolveUpdaterExecutablePath();
                var processStartInfo = new ProcessStartInfo
                {
                    FileName = updaterPath,
                    Arguments = "--restart-agent",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WorkingDirectory = Path.GetDirectoryName(updaterPath) ?? AppContext.BaseDirectory
                };
                processStartInfo.EnvironmentVariables["STORAGEWATCH_AGENT_SERVICE_NAME"] = _serviceName;

                var process = _processStarter(processStartInfo);
                if (process == null)
                {
                    _logger.Log("[AUTOUPDATE] Failed to launch updater for agent restart.");
                    return;
                }

                _logger.Log($"[AUTOUPDATE] Updater launched for agent restart. PID: {process.Id}. Exiting agent process.");
                _exitAction();
            }
            catch (Exception ex)
            {
                _logger.Log($"[AUTOUPDATE] Failed to delegate restart to updater: {ex}");
            }
        }

        private static string ResolveUpdaterExecutablePath()
        {
            var candidates = new[]
            {
                Path.Combine(AppContext.BaseDirectory, "StorageWatch.Updater.exe"),
                Path.Combine(AppContext.BaseDirectory, "StorageWatchUpdater.exe"),
                Path.Combine(AppContext.BaseDirectory, "..", "StorageWatch.Updater.exe"),
                Path.Combine(AppContext.BaseDirectory, "..", "StorageWatchUpdater.exe")
            };

            foreach (var candidate in candidates)
            {
                var fullPath = Path.GetFullPath(candidate);
                if (File.Exists(fullPath))
                    return fullPath;
            }

            throw new FileNotFoundException("Updater executable not found.");
        }
    }
}
