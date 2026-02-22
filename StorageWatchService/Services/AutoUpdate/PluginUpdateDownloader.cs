using Microsoft.Extensions.Logging;
using StorageWatch.Models;
using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace StorageWatch.Services.AutoUpdate
{
    public interface IPluginUpdateDownloader
    {
        Task<PluginDownloadResult> DownloadAsync(PluginUpdateInfo plugin, CancellationToken cancellationToken);
    }

    public class PluginUpdateDownloader : IPluginUpdateDownloader
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<PluginUpdateDownloader> _logger;

        public PluginUpdateDownloader(HttpClient httpClient, ILogger<PluginUpdateDownloader> logger)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<PluginDownloadResult> DownloadAsync(PluginUpdateInfo plugin, CancellationToken cancellationToken)
        {
            if (plugin == null)
                throw new ArgumentNullException(nameof(plugin));

            if (string.IsNullOrWhiteSpace(plugin.DownloadUrl))
            {
                return new PluginDownloadResult
                {
                    Success = false,
                    ErrorMessage = "DownloadUrl is missing from manifest.",
                    Plugin = plugin
                };
            }

            var downloadDirectory = UpdateDownloadHelper.CreateTempDirectory();
            var filePath = Path.Combine(downloadDirectory, "plugin-update.zip");

            try
            {
                var response = await _httpClient.GetAsync(plugin.DownloadUrl, cancellationToken);
                if (!response.IsSuccessStatusCode)
                {
                    return new PluginDownloadResult
                    {
                        Success = false,
                        ErrorMessage = $"Plugin download failed with status {(int)response.StatusCode}.",
                        Plugin = plugin
                    };
                }

                await using (var stream = await response.Content.ReadAsStreamAsync(cancellationToken))
                await using (var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    await stream.CopyToAsync(fileStream, cancellationToken);
                }

                var hashValid = await UpdateDownloadHelper.VerifySha256Async(filePath, plugin.Sha256, cancellationToken);
                if (!hashValid)
                {
                    UpdateDownloadHelper.TryDeleteFile(filePath);
                    return new PluginDownloadResult
                    {
                        Success = false,
                        ErrorMessage = "SHA-256 hash verification failed.",
                        Plugin = plugin
                    };
                }

                return new PluginDownloadResult
                {
                    Success = true,
                    FilePath = filePath,
                    Plugin = plugin
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[AUTOUPDATE] Plugin download failed");
                UpdateDownloadHelper.TryDeleteFile(filePath);
                return new PluginDownloadResult
                {
                    Success = false,
                    ErrorMessage = ex.Message,
                    Plugin = plugin
                };
            }
        }
    }
}
