using Microsoft.Extensions.Logging;
using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using StorageWatchServer.Services.AutoUpdate;

namespace StorageWatchServer.Services
{
    public class UnifiedUpdateClient
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<UnifiedUpdateClient> _logger;

        public UnifiedUpdateClient(HttpClient httpClient, ILogger<UnifiedUpdateClient> logger)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<ServerUnifiedUpdateResult> StartUnifiedUpdateAsync(
            IProgress<ServerUpdateProgressInfo> progress,
            CancellationToken cancellationToken)
        {
            var result = new ServerUnifiedUpdateResult();

            progress.Report(new ServerUpdateProgressInfo { StatusText = "Checking components...", Progress = 0, IsIndeterminate = true });

            var response = await _httpClient.PostAsync("/api/update/unified-install", content: null, cancellationToken);
            var payload = await response.Content.ReadFromJsonAsync<ServerUnifiedInstallResponseDto>(cancellationToken: cancellationToken);

            if (payload?.Progress != null && payload.Progress.Count > 0)
            {
                foreach (var phase in payload.Progress)
                {
                    progress.Report(phase);
                }
            }
            else
            {
                progress.Report(new ServerUpdateProgressInfo { StatusText = "Finalizing update...", Progress = 100, IsIndeterminate = false });
            }

            if (payload == null)
            {
                result.Errors.Add("Unified install response was empty.");
                return result;
            }

            result.ServerUpdated = payload.ServerUpdated;
            result.AgentUpdated = payload.AgentUpdated;
            result.UiUpdated = payload.UiUpdated;
            foreach (var error in payload.Errors)
            {
                result.Errors.Add(error);
            }

            return result;
        }

        public async Task<ServerUpdateStatusDto?> GetStatusAsync(CancellationToken cancellationToken)
        {
            try
            {
                return await _httpClient.GetFromJsonAsync<ServerUpdateStatusDto>("/api/update/status", cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[WEB] Failed to query /api/update/status");
                return null;
            }
        }

        public class ServerUpdateStatusDto
        {
            public string CurrentVersion { get; set; } = string.Empty;
            public string CurrentServerVersion { get; set; } = string.Empty;
            public string CurrentAgentVersion { get; set; } = string.Empty;
            public string CurrentUiVersion { get; set; } = string.Empty;
            public string LatestVersion { get; set; } = string.Empty;
            public string LatestServerVersion { get; set; } = string.Empty;
            public string LatestAgentVersion { get; set; } = string.Empty;
            public string LatestUiVersion { get; set; } = string.Empty;
            public bool UpdateAvailable { get; set; }
            public bool IsInstalling { get; set; }
        }

        public class ServerUnifiedInstallResponseDto
        {
            public bool ServerUpdated { get; set; }
            public bool AgentUpdated { get; set; }
            public bool UiUpdated { get; set; }
            public List<string> Errors { get; set; } = new();
            public List<ServerUpdateProgressInfo> Progress { get; set; } = new();
        }
    }
}
