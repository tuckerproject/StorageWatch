using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.IO.Compression;
using System.Threading;
using System.Threading.Tasks;

namespace StorageWatch.Services.AutoUpdate
{
    public interface IPluginUpdateInstaller
    {
        Task<UpdateInstallResult> InstallAsync(string zipPath, CancellationToken cancellationToken);
    }

    public class PluginUpdateInstaller : IPluginUpdateInstaller
    {
        private readonly ILogger<PluginUpdateInstaller> _logger;
        private readonly string _pluginDirectory;

        public PluginUpdateInstaller(ILogger<PluginUpdateInstaller> logger)
            : this(logger, Path.Combine(AppContext.BaseDirectory, "Plugins"))
        {
        }

        public PluginUpdateInstaller(ILogger<PluginUpdateInstaller> logger, string pluginDirectory)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _pluginDirectory = pluginDirectory ?? throw new ArgumentNullException(nameof(pluginDirectory));
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
                    ErrorMessage = "Plugin package not found."
                });
            }

            var stagingDirectory = Path.Combine(Path.GetTempPath(), "StorageWatchPluginUpdate", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(stagingDirectory);
            Directory.CreateDirectory(_pluginDirectory);

            try
            {
                ZipFile.ExtractToDirectory(zipPath, stagingDirectory, true);

                foreach (var file in Directory.GetFiles(stagingDirectory, "*", SearchOption.AllDirectories))
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var relativePath = Path.GetRelativePath(stagingDirectory, file);
                    var destinationPath = Path.Combine(_pluginDirectory, relativePath);
                    var destinationDir = Path.GetDirectoryName(destinationPath);
                    if (!string.IsNullOrWhiteSpace(destinationDir))
                        Directory.CreateDirectory(destinationDir);

                    File.Copy(file, destinationPath, true);
                }

                _logger.LogInformation("[AUTOUPDATE] Plugin update applied. Reload on next launch.");
                return Task.FromResult(new UpdateInstallResult { Success = true });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[AUTOUPDATE] Plugin install failed");
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
