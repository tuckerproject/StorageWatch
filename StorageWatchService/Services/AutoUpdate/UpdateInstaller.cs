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
            Directory.CreateDirectory(stagingDirectory);

            try
            {
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
                _logger.LogError(ex, "[AUTOUPDATE] Install failed");
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
