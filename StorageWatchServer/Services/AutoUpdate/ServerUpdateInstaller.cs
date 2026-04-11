using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace StorageWatchServer.Services.AutoUpdate
{
    public interface IServerUpdateInstaller
    {
        Task<UpdateInstallResult> InstallAsync(string zipPath, CancellationToken cancellationToken);
    }

    public class ServerUpdateInstaller : IServerUpdateInstaller
    {
        private readonly ILogger<ServerUpdateInstaller> _logger;
        private readonly string _targetDirectory;
        private readonly Func<string, string, bool> _updaterLauncher;
        private readonly Action _gracefulStopAction;
        private readonly Action _exitAction;

        public ServerUpdateInstaller(
            ILogger<ServerUpdateInstaller> logger,
            IServerRestartHandler restartHandler,
            IHostApplicationLifetime lifetime)
            : this(
                logger,
                restartHandler,
                AppContext.BaseDirectory,
                updaterLauncher: null,
                gracefulStopAction: () => lifetime.StopApplication(),
                exitAction: null)
        {
        }

        public ServerUpdateInstaller(
            ILogger<ServerUpdateInstaller> logger,
            IServerRestartHandler restartHandler,
            string targetDirectory,
            Func<string, string, bool>? updaterLauncher = null,
            Action? gracefulStopAction = null,
            Action? exitAction = null)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _ = restartHandler ?? throw new ArgumentNullException(nameof(restartHandler));
            _targetDirectory = targetDirectory ?? throw new ArgumentNullException(nameof(targetDirectory));
            _updaterLauncher = updaterLauncher ?? LaunchUpdaterProcess;
            _gracefulStopAction = gracefulStopAction ?? (() => { });
            _exitAction = exitAction ?? ExitProcess;
        }

        public Task<UpdateInstallResult> InstallAsync(string zipPath, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(zipPath))
                throw new ArgumentException("Zip path is required.", nameof(zipPath));

            if (!File.Exists(zipPath))
            {
                return Task.FromResult(new UpdateInstallResult
                {
                    Success = false,
                    ErrorMessage = "Update package not found."
                });
            }

            var stagingDirectory = Path.Combine(Path.GetTempPath(), "StorageWatchUpdate", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(stagingDirectory);

            try
            {
                cancellationToken.ThrowIfCancellationRequested();

                ZipFile.ExtractToDirectory(zipPath, stagingDirectory, true);

                var installDir = Path.GetFullPath(_targetDirectory);
                var manifestPath = EnsureStagingManifest(stagingDirectory);

                _logger.LogInformation("[AUTOUPDATE] Preparing graceful shutdown before updater handoff.");
                _gracefulStopAction();

                LaunchUpdaterForServerUpdate(stagingDirectory, manifestPath, installDir);

                _logger.LogInformation("[AUTOUPDATE] Server update handoff scheduled. Exiting server process.");
                _exitAction();

                return Task.FromResult(new UpdateInstallResult { Success = true });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[AUTOUPDATE] Server install handoff failed.");
                TryDeleteDirectory(stagingDirectory);
                return Task.FromResult(new UpdateInstallResult
                {
                    Success = false,
                    ErrorMessage = ex.Message
                });
            }
        }

        private void LaunchUpdaterForServerUpdate(string stagingDir, string manifestPath, string installDir)
        {
            if (string.IsNullOrWhiteSpace(stagingDir))
                throw new ArgumentException("Staging directory is required.", nameof(stagingDir));
            if (string.IsNullOrWhiteSpace(manifestPath))
                throw new ArgumentException("Manifest path is required.", nameof(manifestPath));
            if (string.IsNullOrWhiteSpace(installDir))
                throw new ArgumentException("Install directory is required.", nameof(installDir));

            var updaterPath = ResolveUpdaterExecutablePath(installDir);
            var updaterArguments = $"--update-server --source \"{stagingDir}\" --target \"{installDir}\" --manifest \"{manifestPath}\" --restart-server";

            var currentProcessId = Environment.ProcessId;
            var escapedUpdaterPath = updaterPath.Replace("'", "''", StringComparison.Ordinal);
            var escapedArguments = updaterArguments.Replace("'", "''", StringComparison.Ordinal);
            var handoffScript =
                $"$ErrorActionPreference='Stop'; " +
                $"Wait-Process -Id {currentProcessId}; " +
                $"Start-Process -FilePath '{escapedUpdaterPath}' -ArgumentList '{escapedArguments}' -WindowStyle Hidden";

            if (!_updaterLauncher("powershell.exe", $"-NoProfile -NonInteractive -ExecutionPolicy Bypass -Command \"{handoffScript}\""))
                throw new InvalidOperationException("Failed to launch updater handoff process.");
        }

        private static string EnsureStagingManifest(string stagingDirectory)
        {
            var existingManifest = Directory
                .EnumerateFiles(stagingDirectory, "*.json", SearchOption.TopDirectoryOnly)
                .FirstOrDefault(path => string.Equals(Path.GetFileName(path), "manifest.json", StringComparison.OrdinalIgnoreCase));

            if (!string.IsNullOrWhiteSpace(existingManifest))
                return existingManifest;

            var manifestPath = Path.Combine(stagingDirectory, "manifest.json");
            var manifestJson = JsonSerializer.Serialize(new
            {
                component = "server",
                createdUtc = DateTimeOffset.UtcNow
            });
            File.WriteAllText(manifestPath, manifestJson);
            return manifestPath;
        }

        private static string ResolveUpdaterExecutablePath(string installDir)
        {
            var candidates = new[]
            {
                Path.Combine(installDir, "StorageWatchUpdater.exe"),
                Path.Combine(installDir, "StorageWatch.Updater.exe"),
                Path.Combine(AppContext.BaseDirectory, "StorageWatchUpdater.exe"),
                Path.Combine(AppContext.BaseDirectory, "StorageWatch.Updater.exe")
            };

            foreach (var candidate in candidates)
            {
                if (File.Exists(candidate))
                    return candidate;
            }

            throw new FileNotFoundException("Updater executable was not found.");
        }

        private static bool LaunchUpdaterProcess(string fileName, string arguments)
        {
            var process = Process.Start(new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = AppContext.BaseDirectory
            });

            return process != null;
        }

        private static void ExitProcess()
        {
            Environment.Exit(0);
        }

        private static void TryDeleteDirectory(string directory)
        {
            try
            {
                if (Directory.Exists(directory))
                    Directory.Delete(directory, true);
            }
            catch
            {
            }
        }
    }
}
