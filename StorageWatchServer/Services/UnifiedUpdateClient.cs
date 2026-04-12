using Microsoft.Extensions.Logging;
using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using StorageWatchServer.Controllers;

namespace StorageWatchServer.Services
{
    /// <summary>
    /// Client used by the dashboard to query update status and start server updater handoff.
    /// </summary>
    public class UnifiedUpdateClient
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<UnifiedUpdateClient> _logger;

        public UnifiedUpdateClient(HttpClient httpClient, ILogger<UnifiedUpdateClient> logger)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Starts the server update handoff flow through the server update API.
        /// </summary>
        public async Task<UnifiedUpdateStartResult> StartUnifiedUpdateAsync(CancellationToken cancellationToken)
        {
            var result = new UnifiedUpdateStartResult();

            try
            {
                var response = await _httpClient.PostAsync("/api/update/install", content: null, cancellationToken);
                var payload = await response.Content.ReadFromJsonAsync<UpdateInstallResponseDto>(cancellationToken: cancellationToken);

                if (!response.IsSuccessStatusCode || payload == null || !payload.Success)
                {
                    result.Errors.Add(payload?.Error ?? $"Server update request failed with status {(int)response.StatusCode}.");
                    return result;
                }

                result.ServerUpdated = true;
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[WEB] Failed to start server update install flow.");
                result.Errors.Add(ex.Message);
                return result;
            }
        }

        /// <summary>
        /// Gets the latest update status from the server update API.
        /// </summary>
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

        /// <summary>
        /// Result for dashboard-triggered update handoff requests.
        /// </summary>
        public class UnifiedUpdateStartResult
        {
            public bool ServerUpdated { get; set; }
            public bool AgentUpdated { get; set; }
            public bool UiUpdated { get; set; }
            public List<string> Errors { get; set; } = new();
        }
    }
}
