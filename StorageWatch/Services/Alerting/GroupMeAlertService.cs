/// <summary>
/// GroupMe Alert Sender
/// 
/// Implements alert delivery via the GroupMe Bot API. Sends disk space alerts to a GroupMe group
/// by posting messages through a configured bot. This allows team members to receive notifications
/// of disk space issues in their GroupMe chat.
/// </summary>

using System.Net.Http;
using System.Text;
using System.Text.Json;
using StorageWatch.Config.Options;
using StorageWatch.Models;
using StorageWatch.Models.Plugins;
using StorageWatch.Services.Logging;

namespace StorageWatch.Services.Alerting
{
    /// <summary>
    /// Sends alert messages using the GroupMe Bot API.
    /// </summary>
    [AlertSenderPlugin("GroupMe", Description = "Sends alerts via GroupMe Bot API", Version = "2.0.0")]
    public class GroupMeAlertSender : AlertSenderBase, IDisposable
    {
        private readonly GroupMeOptions _options;
        private readonly HttpClient _httpClient;

        public GroupMeAlertSender(GroupMeOptions options, RollingFileLogger logger)
            : base(logger)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _httpClient = new HttpClient();
        }

        /// <inheritdoc/>
        public override string Name => "GroupMe";

        /// <inheritdoc/>
        protected override bool IsEnabled() => _options.Enabled;

        /// <inheritdoc/>
        protected override async Task SendMessageAsync(string message, CancellationToken cancellationToken)
        {
            var payload = new
            {
                bot_id = _options.BotId,
                text = message
            };

            var json = JsonSerializer.Serialize(payload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync(
                "https://api.groupme.com/v3/bots/post",
                content,
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                Logger.Log($"[GroupMe ERROR] Non-success status code: {(int)response.StatusCode} {response.ReasonPhrase}");
            }
        }

        /// <inheritdoc/>
        public override async Task<bool> HealthCheckAsync(CancellationToken cancellationToken = default)
        {
            if (!IsEnabled())
                return false;

            // Basic configuration validation
            if (string.IsNullOrWhiteSpace(_options.BotId))
            {
                Logger.Log("[GroupMe] Health check failed: Missing Bot ID.");
                return false;
            }

            // Optional: Could ping the GroupMe API to verify connectivity
            // For now, we just validate configuration
            return true;
        }

        /// <summary>
        /// Disposes the HTTP client.
        /// </summary>
        public void Dispose()
        {
            _httpClient?.Dispose();
        }
    }
}