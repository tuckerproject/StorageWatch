/// <summary>
/// Central Server Forwarder Service
/// 
/// This service handles the asynchronous forwarding of disk space log entries to a central server.
/// It runs non-blocking HTTP POST requests to forward each log entry, with no retry logic or queuing.
/// This is designed for agent mode, where the local service reports to a central aggregation point.
/// </summary>

using StorageWatch.Config.Options;
using StorageWatch.Models;
using StorageWatch.Services.Logging;
using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace StorageWatch.Services.CentralServer
{
    /// <summary>
    /// Forwards disk space log entries to a central server via HTTP API.
    /// This service operates in agent mode and is responsible for sending local disk data
    /// to a remote aggregation point. All operations are non-blocking and fire-and-forget.
    /// </summary>
    public class CentralServerForwarder
    {
        private readonly CentralServerOptions _options;
        private readonly RollingFileLogger _logger;
        private readonly HttpClient _httpClient;

        /// <summary>
        /// Initializes a new instance of the CentralServerForwarder class.
        /// </summary>
        /// <param name="options">The central server options containing the server URL and API key.</param>
        /// <param name="logger">The logger for recording forwarding operations and errors.</param>
        public CentralServerForwarder(CentralServerOptions options, RollingFileLogger logger)
        {
            _options = options;
            _logger = logger;
            _httpClient = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(10)
            };
        }

        /// <summary>
        /// Forwards a disk space log entry to the central server asynchronously without blocking.
        /// This method fires a non-blocking POST request and does not wait for the response.
        /// </summary>
        /// <param name="entry">The disk space log entry to forward.</param>
        public void ForwardLogEntryAsync(DiskSpaceLogEntry entry)
        {
            // Fire off the async operation without awaiting to avoid blocking the caller
            _ = SendLogEntryAsync(entry);
        }

        /// <summary>
        /// Internal method that actually sends the log entry to the central server.
        /// </summary>
        /// <param name="entry">The disk space log entry to send.</param>
        private async Task SendLogEntryAsync(DiskSpaceLogEntry entry)
        {
            try
            {
                // Skip if server URL is not configured
                if (string.IsNullOrWhiteSpace(_options.ServerUrl))
                {
                    _logger.Log("[CentralServerForwarder] WARNING: ServerUrl is not configured. Skipping forward.");
                    return;
                }

                // Build the endpoint URL
                string endpoint = $"{_options.ServerUrl.TrimEnd('/')}/api/logs/disk-space";

                // Serialize the entry to JSON
                var json = JsonSerializer.Serialize(entry);
                var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

                // Prepare the request with optional API key header
                using var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
                {
                    Content = content
                };

                if (!string.IsNullOrWhiteSpace(_options.ApiKey))
                {
                    request.Headers.Add("X-API-Key", _options.ApiKey);
                }

                // Send the request
                var response = await _httpClient.SendAsync(request);

                if (response.IsSuccessStatusCode)
                {
                    _logger.Log(
                        $"[CentralServerForwarder SUCCESS] Forwarded {entry.AgentMachineName}/{entry.DriveLetter} to {endpoint}"
                    );
                }
                else if ((int)response.StatusCode == 401 || (int)response.StatusCode == 403)
                {
                    _logger.Log(
                        $"[CentralServerForwarder AUTH ERROR] Authentication failed when forwarding {entry.AgentMachineName}/{entry.DriveLetter}: {response.StatusCode}"
                    );
                }
                else
                {
                    _logger.Log(
                        $"[CentralServerForwarder ERROR] Failed to forward {entry.AgentMachineName}/{entry.DriveLetter}: HTTP {response.StatusCode}"
                    );
                }
            }
            catch (HttpRequestException ex)
            {
                _logger.Log(
                    $"[CentralServerForwarder UNREACHABLE] Could not reach central server at {_options.ServerUrl}: {ex.Message}"
                );
            }
            catch (TaskCanceledException ex)
            {
                _logger.Log(
                    $"[CentralServerForwarder TIMEOUT] Request to central server timed out: {ex.Message}"
                );
            }
            catch (Exception ex)
            {
                _logger.Log(
                    $"[CentralServerForwarder ERROR] Unexpected error when forwarding log entry: {ex}"
                );
            }
        }

        /// <summary>
        /// Determines whether the forwarder is configured and ready to forward data.
        /// </summary>
        /// <returns>True if the forwarder is enabled and has a valid server URL; otherwise false.</returns>
        public bool IsEnabled()
        {
            return _options.Enabled &&
                   _options.Mode.Equals("Agent", StringComparison.OrdinalIgnoreCase) &&
                   !string.IsNullOrWhiteSpace(_options.ServerUrl);
        }

        /// <summary>
        /// Disposes the HTTP client resources.
        /// </summary>
        public void Dispose()
        {
            _httpClient?.Dispose();
        }
    }
}
