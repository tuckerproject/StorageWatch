using Microsoft.Extensions.Logging;
using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace StorageWatchUI.Services.AutoUpdate
{
    /// <summary>
    /// Installs UI updates using a handoff-only pipeline: prepare, stage, handoff, exit.
    /// </summary>
    public interface IUiUpdateInstaller
    {
        /// <summary>
        /// Prepares and stages the update package, then launches the updater executable and exits the UI process.
        /// </summary>
        Task<UpdateInstallResult> InstallAsync(string zipPath, CancellationToken cancellationToken, IProgress<double>? progress = null);
    }

    /// <summary>
    /// UI update installer that only prepares files, stages payload content, hands off to updater, and exits.
    /// </summary>
    public class UiUpdateHandoffInstaller : IUiUpdateInstaller
    {
        private readonly ILogger<UiUpdateHandoffInstaller> _logger;
        private readonly string _targetDirectory;
        private readonly Func<string, string, bool> _updaterLauncher;
        private readonly Action _exitAction;

        public UiUpdateHandoffInstaller(
            ILogger<UiUpdateHandoffInstaller> logger)
            : this(logger, AppContext.BaseDirectory)
        {
        }

        public UiUpdateHandoffInstaller(
            ILogger<UiUpdateHandoffInstaller> logger,
            string targetDirectory,
            Func<string, string, bool>? updaterLauncher = null,
            Action? exitAction = null)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _targetDirectory = targetDirectory ?? throw new ArgumentNullException(nameof(targetDirectory));
            _updaterLauncher = updaterLauncher ?? LaunchUpdaterProcess;
            _exitAction = exitAction ?? ExitApplication;
        }

        /// <summary>
        /// Executes the handoff flow: prepare package input, stage extracted payload, hand off to updater, and exit.
        /// </summary>
        public Task<UpdateInstallResult> InstallAsync(string zipPath, CancellationToken cancellationToken, IProgress<double>? progress = null)
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
                progress?.Report(0.8);

                var installDir = Path.GetFullPath(_targetDirectory);
                var manifestPath = EnsureStagingManifest(stagingDirectory);

                LaunchUpdaterForUIUpdate(stagingDirectory, manifestPath, installDir);

                progress?.Report(1.0);
                _logger.LogInformation("[AUTOUPDATE] UI update handed off to updater executable.");

                _exitAction();
                return Task.FromResult(new UpdateInstallResult { Success = true });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[AUTOUPDATE] UI install handoff failed");
                return Task.FromResult(new UpdateInstallResult
                {
                    Success = false,
                    ErrorMessage = ex.Message
                });
            }
        }

        private void LaunchUpdaterForUIUpdate(string stagingDir, string manifestPath, string installDir)
        {
            if (string.IsNullOrWhiteSpace(stagingDir))
                throw new ArgumentException("Staging directory is required.", nameof(stagingDir));
            if (string.IsNullOrWhiteSpace(manifestPath))
                throw new ArgumentException("Manifest path is required.", nameof(manifestPath));
            if (string.IsNullOrWhiteSpace(installDir))
                throw new ArgumentException("Install directory is required.", nameof(installDir));

            var updaterPath = ResolveUpdaterExecutablePath(installDir);
            var arguments = $"--update-ui --source \"{stagingDir}\" --target \"{installDir}\" --manifest \"{manifestPath}\" --restart-ui";

            if (!_updaterLauncher(updaterPath, arguments))
            {
                throw new InvalidOperationException("Failed to launch updater executable.");
            }
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
                component = "ui",
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

        private static bool LaunchUpdaterProcess(string updaterPath, string arguments)
        {
            var process = Process.Start(new ProcessStartInfo
            {
                FileName = updaterPath,
                Arguments = arguments,
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = Path.GetDirectoryName(updaterPath) ?? AppContext.BaseDirectory
            });

            return process != null;
        }

        private static void ExitApplication()
        {
            if (Application.Current == null)
            {
                Environment.Exit(0);
                return;
            }

            Application.Current.Dispatcher.Invoke(() => Application.Current.Shutdown(0));
        }
    }
}
