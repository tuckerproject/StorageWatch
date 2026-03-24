using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.IO.Compression;
using System.Threading;
using System.Threading.Tasks;

namespace StorageWatch.Services.AutoUpdate
{
    public interface IServiceUpdateInstaller
    {
        Task<UpdateInstallResult> InstallAsync(string zipPath, CancellationToken cancellationToken);
    }

    public class ServiceUpdateInstaller : IServiceUpdateInstaller
    {
        private readonly ILogger<ServiceUpdateInstaller> _logger;
        private readonly IServiceRestartHandler _restartHandler;
        private readonly string _targetDirectory;

        public ServiceUpdateInstaller(ILogger<ServiceUpdateInstaller> logger, IServiceRestartHandler restartHandler)
            : this(logger, restartHandler, AppContext.BaseDirectory)
        {
        }

        public ServiceUpdateInstaller(ILogger<ServiceUpdateInstaller> logger, IServiceRestartHandler restartHandler, string targetDirectory)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _restartHandler = restartHandler ?? throw new ArgumentNullException(nameof(restartHandler));
            _targetDirectory = targetDirectory ?? throw new ArgumentNullException(nameof(targetDirectory));
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
            var backupDirectory = Path.Combine(Path.GetTempPath(), "StorageWatchBackup", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(stagingDirectory);
            Directory.CreateDirectory(backupDirectory);

            try
            {
                _logger.LogInformation("[AUTOUPDATE] Creating backup before installation.");
                CopyDirectory(_targetDirectory, backupDirectory, cancellationToken);

                ZipFile.ExtractToDirectory(zipPath, stagingDirectory, true);

                foreach (var file in Directory.GetFiles(stagingDirectory, "*", SearchOption.AllDirectories))
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var relativePath = Path.GetRelativePath(stagingDirectory, file);
                    var destinationPath = Path.Combine(_targetDirectory, relativePath);
                    var destinationDir = Path.GetDirectoryName(destinationPath);
                    if (!string.IsNullOrWhiteSpace(destinationDir))
                        Directory.CreateDirectory(destinationDir);

                    File.Copy(file, destinationPath, true);
                }

                _logger.LogInformation("[AUTOUPDATE] Update package applied. Restarting service to complete update.");
                _restartHandler.RequestRestart();
                return Task.FromResult(new UpdateInstallResult { Success = true });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[AUTOUPDATE] Install failed. Starting rollback.");

                try
                {
                    RestoreBackup(backupDirectory, _targetDirectory, cancellationToken);
                    _logger.LogInformation("[AUTOUPDATE] Rollback completed successfully.");
                }
                catch (Exception rollbackEx)
                {
                    _logger.LogError(rollbackEx, "[AUTOUPDATE] Rollback failed.");
                }

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

                try
                {
                    if (Directory.Exists(backupDirectory))
                        Directory.Delete(backupDirectory, true);
                }
                catch
                {
                }
            }
        }

        private static void RestoreBackup(string backupDirectory, string targetDirectory, CancellationToken cancellationToken)
        {
            if (Directory.Exists(targetDirectory))
            {
                foreach (var entry in Directory.GetFileSystemEntries(targetDirectory))
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    if (Directory.Exists(entry))
                    {
                        Directory.Delete(entry, true);
                    }
                    else
                    {
                        File.Delete(entry);
                    }
                }
            }

            CopyDirectory(backupDirectory, targetDirectory, cancellationToken);
        }

        private static void CopyDirectory(string sourceDirectory, string destinationDirectory, CancellationToken cancellationToken)
        {
            Directory.CreateDirectory(destinationDirectory);

            foreach (var directory in Directory.GetDirectories(sourceDirectory, "*", SearchOption.AllDirectories))
            {
                cancellationToken.ThrowIfCancellationRequested();
                var relativePath = Path.GetRelativePath(sourceDirectory, directory);
                Directory.CreateDirectory(Path.Combine(destinationDirectory, relativePath));
            }

            foreach (var file in Directory.GetFiles(sourceDirectory, "*", SearchOption.AllDirectories))
            {
                cancellationToken.ThrowIfCancellationRequested();
                var relativePath = Path.GetRelativePath(sourceDirectory, file);
                var destinationFile = Path.Combine(destinationDirectory, relativePath);
                var destinationDir = Path.GetDirectoryName(destinationFile);
                if (!string.IsNullOrWhiteSpace(destinationDir))
                    Directory.CreateDirectory(destinationDir);

                File.Copy(file, destinationFile, true);
            }
        }
    }
}
