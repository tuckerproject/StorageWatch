using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace StorageWatchUI.Services.AutoUpdate
{
    public interface IUiUpdateInstaller
    {
        Task<UpdateInstallResult> InstallAsync(string zipPath, CancellationToken cancellationToken, bool promptForRestart = true, IProgress<double>? progress = null);
    }

    public class UiUpdateInstaller : IUiUpdateInstaller
    {
        private readonly ILogger<UiUpdateInstaller> _logger;
        private readonly IUiRestartPrompter _restartPrompter;
        private readonly IUiRestartHandler _restartHandler;
        private readonly string _targetDirectory;

        public UiUpdateInstaller(
            ILogger<UiUpdateInstaller> logger,
            IUiRestartPrompter restartPrompter,
            IUiRestartHandler restartHandler)
            : this(logger, restartPrompter, restartHandler, AppContext.BaseDirectory)
        {
        }

        public UiUpdateInstaller(
            ILogger<UiUpdateInstaller> logger,
            IUiRestartPrompter restartPrompter,
            IUiRestartHandler restartHandler,
            string targetDirectory)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _restartPrompter = restartPrompter ?? throw new ArgumentNullException(nameof(restartPrompter));
            _restartHandler = restartHandler ?? throw new ArgumentNullException(nameof(restartHandler));
            _targetDirectory = targetDirectory ?? throw new ArgumentNullException(nameof(targetDirectory));
        }

        public Task<UpdateInstallResult> InstallAsync(string zipPath, CancellationToken cancellationToken, bool promptForRestart = true, IProgress<double>? progress = null)
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
                ZipFile.ExtractToDirectory(zipPath, stagingDirectory, true);

                var files = Directory.GetFiles(stagingDirectory, "*", SearchOption.AllDirectories);
                var totalFiles = files.Length;
                var copiedFiles = 0;

                foreach (var file in files)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var relativePath = Path.GetRelativePath(stagingDirectory, file);
                    var destinationPath = Path.Combine(_targetDirectory, relativePath);
                    var destinationDir = Path.GetDirectoryName(destinationPath);
                    if (!string.IsNullOrWhiteSpace(destinationDir))
                        Directory.CreateDirectory(destinationDir);

                    File.Copy(file, destinationPath, true);

                    copiedFiles++;
                    if (totalFiles > 0)
                        progress?.Report((double)copiedFiles / totalFiles);
                }

                progress?.Report(1.0);

                _logger.LogInformation("[AUTOUPDATE] UI update applied.");
                if (promptForRestart && _restartPrompter.PromptForRestart())
                {
                    _restartHandler.RequestRestart();
                }

                return Task.FromResult(new UpdateInstallResult { Success = true });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[AUTOUPDATE] UI install failed");
                return Task.FromResult(new UpdateInstallResult
                {
                    Success = false,
                    ErrorMessage = ex.Message
                });
            }
            finally
            {
                try
                {
                    if (Directory.Exists(stagingDirectory))
                        Directory.Delete(stagingDirectory, true);
                }
                catch
                {
                }
            }
        }
    }
}
