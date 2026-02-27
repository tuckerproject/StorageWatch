using StorageWatch.Config.Options;
using StorageWatch.Models;
using StorageWatch.Services.Logging;
using System.Net;
using System.Text;
using System.Text.Json;

namespace StorageWatch.Services.CentralServer
{
    public class AgentReportSender
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        private readonly HttpClient _httpClient;
        private readonly RollingFileLogger _logger;
        private readonly IReadOnlyList<TimeSpan> _retryDelays;

        public AgentReportSender(
            HttpClient httpClient,
            RollingFileLogger logger,
            IReadOnlyList<TimeSpan>? retryDelays = null)
        {
            _httpClient = httpClient;
            _logger = logger;
            _retryDelays = retryDelays ?? new[]
            {
                TimeSpan.FromSeconds(5),
                TimeSpan.FromSeconds(15),
                TimeSpan.FromSeconds(30)
            };
        }

        public async Task<bool> SendReportAsync(
            AgentReportRequest report,
            CentralServerOptions options,
            CancellationToken cancellationToken)
        {
            var endpoint = BuildEndpoint(options.ServerUrl);
            var payload = JsonSerializer.Serialize(report, JsonOptions);

            for (var attempt = 0; attempt <= _retryDelays.Count; attempt++)
            {
                try
                {
                    using var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
                    {
                        Content = new StringContent(payload, Encoding.UTF8, "application/json")
                    };

                    if (!string.IsNullOrWhiteSpace(options.ApiKey))
                    {
                        request.Headers.Add("X-API-Key", options.ApiKey);
                    }

                    var response = await _httpClient.SendAsync(request, cancellationToken);

                    if (response.IsSuccessStatusCode)
                    {
                        _logger.Log($"[AGENT REPORT] Report sent to {endpoint}.");
                        return true;
                    }

                    if (response.StatusCode >= HttpStatusCode.BadRequest && response.StatusCode < HttpStatusCode.InternalServerError)
                    {
                        _logger.Log($"[AGENT REPORT] Server rejected report with {(int)response.StatusCode} {response.ReasonPhrase}.");
                        return false;
                    }

                    _logger.Log($"[AGENT REPORT] Server error {(int)response.StatusCode} {response.ReasonPhrase}.");
                }
                catch (HttpRequestException ex)
                {
                    _logger.Log($"[AGENT REPORT] Server unreachable: {ex.Message}");
                }
                catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
                {
                    _logger.Log($"[AGENT REPORT] Request timed out: {ex.Message}");
                }

                if (attempt >= _retryDelays.Count)
                {
                    break;
                }

                var delay = _retryDelays[attempt];
                _logger.Log($"[AGENT REPORT] Retry attempt {attempt + 1} in {delay.TotalSeconds} seconds.");
                await Task.Delay(delay, cancellationToken);
            }

            _logger.Log("[AGENT REPORT] Failed to send report after retries.");
            return false;
        }

        private static string BuildEndpoint(string serverUrl)
        {
            return $"{serverUrl.TrimEnd('/')}/api/agent/report";
        }
    }
}
