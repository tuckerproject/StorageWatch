using StorageWatch.Services.Logging;
using Microsoft.Extensions.Hosting;
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace StorageWatch.Services.AutoUpdate
{
    public interface IServiceRestartHandler
    {
        void RequestRestart();
    }

    public class ScmServiceRestartHandler : IServiceRestartHandler
    {
        private const string DefaultServiceName = "StorageWatchAgent";
        private static readonly TimeSpan RestartTimeout = TimeSpan.FromMinutes(2);
        private readonly RollingFileLogger _logger;
        private readonly IHostApplicationLifetime _lifetime;
        private readonly string _serviceName;

        public ScmServiceRestartHandler(RollingFileLogger logger, IHostApplicationLifetime lifetime)
            : this(logger, lifetime, Environment.GetEnvironmentVariable("STORAGEWATCH_AGENT_SERVICE_NAME"))
        {
        }

        internal ScmServiceRestartHandler(
            RollingFileLogger logger,
            IHostApplicationLifetime lifetime,
            string? serviceName)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _lifetime = lifetime ?? throw new ArgumentNullException(nameof(lifetime));
            _serviceName = string.IsNullOrWhiteSpace(serviceName) ? DefaultServiceName : serviceName.Trim();
        }

        public void RequestRestart()
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                _logger.Log("[AUTOUPDATE] SCM restart requested on non-Windows OS. Restart skipped.");
                return;
            }

            try
            {
                var helperScript = BuildRestartHelperScript(_serviceName, RestartTimeout);
                var helperProcess = StartHelperProcess(new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = $"-NoProfile -NonInteractive -ExecutionPolicy Bypass -Command \"{helperScript}\"",
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    WindowStyle = ProcessWindowStyle.Hidden
                });

                if (helperProcess == null)
                {
                    _logger.Log($"[AUTOUPDATE] Failed to launch SCM restart helper for service '{_serviceName}'.");
                    return;
                }

                _logger.Log($"[AUTOUPDATE] Restart requested for service '{_serviceName}'. Helper PID: {helperProcess.Id}. Initiating graceful shutdown.");
                _lifetime.StopApplication();
            }
            catch (Exception ex)
            {
                _logger.Log($"[AUTOUPDATE] Service restart failed for '{_serviceName}': {ex}");
            }
        }

        protected virtual Process? StartHelperProcess(ProcessStartInfo processStartInfo)
        {
            return Process.Start(processStartInfo);
        }

        private static string BuildRestartHelperScript(string serviceName, TimeSpan timeout)
        {
            var escapedServiceName = serviceName.Replace("'", "''", StringComparison.Ordinal);
            var timeoutSeconds = (int)timeout.TotalSeconds;

            return string.Join(';',
                "$ErrorActionPreference='Stop'",
                $"$serviceName='{escapedServiceName}'",
                $"$timeout=[TimeSpan]::FromSeconds({timeoutSeconds})",
                "$deadline=(Get-Date).Add($timeout)",
                "Stop-Service -Name $serviceName -ErrorAction Stop",
                "do { Start-Sleep -Seconds 1; $service=Get-Service -Name $serviceName -ErrorAction Stop } while ($service.Status -ne 'Stopped' -and (Get-Date) -lt $deadline)",
                "if ($service.Status -ne 'Stopped') { throw \"Timed out waiting for service to stop.\" }",
                "Start-Service -Name $serviceName -ErrorAction Stop",
                "$deadline=(Get-Date).Add($timeout)",
                "do { Start-Sleep -Seconds 1; $service=Get-Service -Name $serviceName -ErrorAction Stop } while ($service.Status -ne 'Running' -and (Get-Date) -lt $deadline)",
                "if ($service.Status -ne 'Running') { throw \"Timed out waiting for service to start.\" }");
        }
    }
}
