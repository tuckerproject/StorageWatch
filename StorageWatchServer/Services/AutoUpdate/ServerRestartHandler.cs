using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using StorageWatchServer.Services.Logging;
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace StorageWatchServer.Services.AutoUpdate
{
    public interface IServerRestartHandler
    {
        void RequestRestart();
    }

    public class ServerRestartHandler : IServerRestartHandler
    {
        private const string DefaultServiceName = "StorageWatchServer";
        private static readonly TimeSpan RestartTimeout = TimeSpan.FromMinutes(2);
        private readonly ILogger<ServerRestartHandler> _logger;
        private readonly IHostApplicationLifetime _lifetime;
        private readonly string _serviceName;
        private readonly string _helperLogPath;

        public ServerRestartHandler(ILogger<ServerRestartHandler> logger, IHostApplicationLifetime lifetime)
            : this(
                logger,
                lifetime,
                Environment.GetEnvironmentVariable("STORAGEWATCH_SERVER_SERVICE_NAME"),
                LogDirectoryInitializer.GetLogFilePath("server-restart.log"))
        {
        }

        internal ServerRestartHandler(
            ILogger<ServerRestartHandler> logger,
            IHostApplicationLifetime lifetime,
            string? serviceName,
            string helperLogPath)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _lifetime = lifetime ?? throw new ArgumentNullException(nameof(lifetime));
            _serviceName = string.IsNullOrWhiteSpace(serviceName) ? DefaultServiceName : serviceName.Trim();
            _helperLogPath = string.IsNullOrWhiteSpace(helperLogPath)
                ? LogDirectoryInitializer.GetLogFilePath("server-restart.log")
                : helperLogPath;
        }

        public void RequestRestart()
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                _logger.LogWarning("SCM restart requested for server service '{ServiceName}' on non-Windows OS. Restart skipped.", _serviceName);
                return;
            }

            try
            {
                var helperScript = BuildRestartHelperScript(_serviceName, RestartTimeout, _helperLogPath);
                var encodedScript = Convert.ToBase64String(Encoding.Unicode.GetBytes(helperScript));

                _logger.LogInformation("Preparing SCM restart for server service '{ServiceName}'. Detailed helper output will be written to '{HelperLogPath}'.", _serviceName, _helperLogPath);
                _logger.LogInformation("Launching SCM restart helper for server service '{ServiceName}'.", _serviceName);

                var helperProcess = StartHelperProcess(new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = $"-NoProfile -NonInteractive -ExecutionPolicy Bypass -EncodedCommand {encodedScript}",
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    WindowStyle = ProcessWindowStyle.Hidden
                });

                if (helperProcess == null)
                {
                    _logger.LogError("Failed to launch SCM restart helper for server service '{ServiceName}'.", _serviceName);
                    return;
                }

                _logger.LogInformation("SCM restart helper launched for server service '{ServiceName}' with PID {HelperProcessId}.", _serviceName, helperProcess.Id);
                _logger.LogInformation("Requesting graceful shutdown for server service '{ServiceName}' via StopApplication().", _serviceName);
                _lifetime.StopApplication();
                _logger.LogInformation("StopApplication() issued for server service '{ServiceName}'. The SCM restart helper is now waiting for hosted services to stop before restarting the service.", _serviceName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SCM restart flow failed for server service '{ServiceName}'.", _serviceName);
            }
        }

        protected virtual Process? StartHelperProcess(ProcessStartInfo processStartInfo)
        {
            return Process.Start(processStartInfo);
        }

        private static string BuildRestartHelperScript(string serviceName, TimeSpan timeout, string helperLogPath)
        {
            var escapedServiceName = serviceName.Replace("'", "''", StringComparison.Ordinal);
            var escapedHelperLogPath = helperLogPath.Replace("'", "''", StringComparison.Ordinal);
            var timeoutSeconds = (int)timeout.TotalSeconds;

            var script = new StringBuilder();
            script.AppendLine("$ErrorActionPreference='Stop'");
            script.AppendLine($"$serviceName='{escapedServiceName}'");
            script.AppendLine($"$timeout=[TimeSpan]::FromSeconds({timeoutSeconds})");
            script.AppendLine($"$helperLogPath='{escapedHelperLogPath}'");
            script.AppendLine();
            script.AppendLine("function Write-Log {");
            script.AppendLine("    param([string]$message)");
            script.AppendLine("    $timestamp = (Get-Date).ToString('yyyy-MM-dd HH:mm:ss')");
            script.AppendLine("    Add-Content -Path $helperLogPath -Value \"$timestamp  [AUTOUPDATE] $message\"");
            script.AppendLine("}");
            script.AppendLine();
            script.AppendLine("function Get-ServiceState {");
            script.AppendLine("    $output = & sc.exe query $serviceName 2>&1 | Out-String");
            script.AppendLine("    if ($LASTEXITCODE -ne 0) {");
            script.AppendLine("        throw \"sc.exe query failed for service '$serviceName': $output\"");
            script.AppendLine("    }");
            script.AppendLine();
            script.AppendLine("    $match = [regex]::Match($output, 'STATE\\s*:\\s*\\d+\\s+([A-Z_]+)')");
            script.AppendLine("    if (-not $match.Success) {");
            script.AppendLine("        throw \"Unable to determine service state for '$serviceName'. Output: $output\"");
            script.AppendLine("    }");
            script.AppendLine();
            script.AppendLine("    return $match.Groups[1].Value");
            script.AppendLine("}");
            script.AppendLine();
            script.AppendLine("function Wait-ForState {");
            script.AppendLine("    param([string]$expectedState, [datetime]$deadline)");
            script.AppendLine("    do {");
            script.AppendLine("        $state = Get-ServiceState");
            script.AppendLine("        if ($state -eq $expectedState) {");
            script.AppendLine("            return $true");
            script.AppendLine("        }");
            script.AppendLine();
            script.AppendLine("        Start-Sleep -Seconds 1");
            script.AppendLine("    } while ((Get-Date) -lt $deadline)");
            script.AppendLine();
            script.AppendLine("    return (Get-ServiceState) -eq $expectedState");
            script.AppendLine("}");
            script.AppendLine();
            script.AppendLine("try {");
            script.AppendLine("    Write-Log \"Restart helper started for service '$serviceName'. Waiting for graceful shutdown after StopApplication().\"");
            script.AppendLine("    $stopDeadline = (Get-Date).Add($timeout)");
            script.AppendLine();
            script.AppendLine("    if (-not (Wait-ForState -expectedState 'STOPPED' -deadline $stopDeadline)) {");
            script.AppendLine("        Write-Log \"Service '$serviceName' did not report STOPPED after the graceful shutdown window. Attempting 'sc.exe stop'.\"");
            script.AppendLine("        $stopOutput = & sc.exe stop $serviceName 2>&1 | Out-String");
            script.AppendLine("        if ($LASTEXITCODE -ne 0 -and $stopOutput -notmatch 'FAILED 1062') {");
            script.AppendLine("            throw \"sc.exe stop failed for service '$serviceName': $stopOutput\"");
            script.AppendLine("        }");
            script.AppendLine();
            script.AppendLine("        Write-Log \"Issued 'sc.exe stop' for service '$serviceName'. Waiting for STOPPED state.\"");
            script.AppendLine("        $stopDeadline = (Get-Date).Add($timeout)");
            script.AppendLine("        if (-not (Wait-ForState -expectedState 'STOPPED' -deadline $stopDeadline)) {");
            script.AppendLine("            $state = Get-ServiceState");
            script.AppendLine("            throw \"Timed out waiting for service '$serviceName' to stop. Current state: $state\"");
            script.AppendLine("        }");
            script.AppendLine("    }");
            script.AppendLine();
            script.AppendLine("    Write-Log \"Service '$serviceName' reported STOPPED. Attempting 'sc.exe start'.\"");
            script.AppendLine("    $startOutput = & sc.exe start $serviceName 2>&1 | Out-String");
            script.AppendLine("    if ($LASTEXITCODE -ne 0) {");
            script.AppendLine("        throw \"sc.exe start failed for service '$serviceName': $startOutput\"");
            script.AppendLine("    }");
            script.AppendLine();
            script.AppendLine("    Write-Log \"Issued 'sc.exe start' for service '$serviceName'. Waiting for RUNNING state.\"");
            script.AppendLine("    $startDeadline = (Get-Date).Add($timeout)");
            script.AppendLine("    if (-not (Wait-ForState -expectedState 'RUNNING' -deadline $startDeadline)) {");
            script.AppendLine("        $state = Get-ServiceState");
            script.AppendLine("        throw \"Timed out waiting for service '$serviceName' to start. Current state: $state\"");
            script.AppendLine("    }");
            script.AppendLine();
            script.AppendLine("    Write-Log \"Service '$serviceName' reported RUNNING. SCM restart completed successfully.\"");
            script.AppendLine("}");
            script.AppendLine("catch {");
            script.AppendLine("    Write-Log \"Restart helper failed for service '$serviceName': $($_.Exception.Message)\"");
            script.AppendLine("    throw");
            script.AppendLine("}");

            return script.ToString();
        }
    }
}
