using StorageWatchUI.Models;
using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace StorageWatchUI.Services.AutoUpdate
{
    public interface IUiUpdateDownloader
    {
        Task<UpdateDownloadResult> DownloadAsync(ComponentUpdateInfo component, CancellationToken cancellationToken);
    }

    public class UiUpdateDownloader : IUiUpdateDownloader
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<UiUpdateDownloader> _logger;

        public UiUpdateDownloader(HttpClient httpClient, ILogger<UiUpdateDownloader> logger)
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
