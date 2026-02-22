using StorageWatchServer.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace StorageWatchServer.Services.AutoUpdate
{
    public interface IServerUpdateDownloader
    {
        Task<UpdateDownloadResult> DownloadAsync(ComponentUpdateInfo component, CancellationToken cancellationToken);
    }

    public class ServerUpdateDownloader : IServerUpdateDownloader
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<ServerUpdateDownloader> _logger;

        public ServerUpdateDownloader(HttpClient httpClient, ILogger<ServerUpdateDownloader> logger)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<UpdateDownloadResult> DownloadAsync(ComponentUpdateInfo component, CancellationToken cancellationToken)
        {
            if (component == null)
                throw new ArgumentNullException(nameof(component));

            if (string.IsNullOrWhiteSpace(component.DownloadUrl))
            {
                return new UpdateDownloadResult
                {
                    Success = false,
                    ErrorMessage = "DownloadUrl is missing from manifest."
                };
            }

            var downloadDirectory = UpdateDownloadHelper.CreateTempDirectory();
            var filePath = Path.Combine(downloadDirectory, "update.zip");

            try
            {
                var response = await _httpClient.GetAsync(component.DownloadUrl, cancellationToken);
                if (!response.IsSuccessStatusCode)
                {
                    return new UpdateDownloadResult
                    {
                        Success = false,
                        ErrorMessage = $"Update download failed with status {(int)response.StatusCode}."
                    };
                }

                await using (var stream = await response.Content.ReadAsStreamAsync(cancellationToken))
                await using (var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    await stream.CopyToAsync(fileStream, cancellationToken);
                }

                var hashValid = await UpdateDownloadHelper.VerifySha256Async(filePath, component.Sha256, cancellationToken);
                if (!hashValid)
                {
                    UpdateDownloadHelper.TryDeleteFile(filePath);
                    return new UpdateDownloadResult
                    {
                        Success = false,
                        ErrorMessage = "SHA-256 hash verification failed."
                    };
                }

                return new UpdateDownloadResult
                {
                    Success = true,
                    FilePath = filePath
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[AUTOUPDATE] Download failed");
                UpdateDownloadHelper.TryDeleteFile(filePath);
                return new UpdateDownloadResult
                {
                    Success = false,
                    ErrorMessage = ex.Message
                };
            }
        }
    }
}
