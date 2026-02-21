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

        /// <summary>
        /// Formats a DiskStatus into an alert message with a timestamp to ensure uniqueness in GroupMe.
        /// Overrides the base implementation to add a timestamp that prevents message suppression.
        /// </summary>
        /// <param name="status">The disk status to format.</param>
        /// <returns>A formatted alert message with timestamp.</returns>
        protected override string FormatMessage(DiskStatus status)
        {
            var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            var timeZone = TimeZoneInfo.Local.StandardName;

            // Use the CurrentState set by NotificationLoop instead of re-evaluating
            if (!string.IsNullOrEmpty(status.CurrentState))
            {
                if (status.CurrentState == "NOT_READY")
                {
                    return $"ALERT — {Environment.MachineName}: Drive {status.DriveName} is NOT READY or unavailable. Time: {timestamp} {timeZone}";
                }
                else if (status.CurrentState == "ALERT")
                {
                    return $"ALERT — {Environment.MachineName}: Drive {status.DriveName} is below threshold. " +
                           $"{status.FreeSpaceGb:F2} GB free ({status.PercentFree:F2}%). Time: {timestamp} {timeZone}";
                }
                else if (status.CurrentState == "NORMAL")
                {
                    return $"RECOVERY — {Environment.MachineName}: Drive {status.DriveName} has recovered. " +
                           $"{status.FreeSpaceGb:F2} GB free ({status.PercentFree:F2}%). Time: {timestamp} {timeZone}";
                }
            }

            // Fallback if CurrentState is not set (for backwards compatibility)
            if (status.TotalSpaceGb == 0)
            {
                return $"ALERT — {Environment.MachineName}: Drive {status.DriveName} is NOT READY or unavailable. Time: {timestamp} {timeZone}";
            }

            if (status.PercentFree < 10)
            {
                return $"ALERT — {Environment.MachineName}: Drive {status.DriveName} is below threshold. " +
                       $"{status.FreeSpaceGb:F2} GB free ({status.PercentFree:F2}%). Time: {timestamp} {timeZone}";
            }
            else
            {
                return $"RECOVERY — {Environment.MachineName}: Drive {status.DriveName} has recovered. " +
                       $"{status.FreeSpaceGb:F2} GB free ({status.PercentFree:F2}%). Time: {timestamp} {timeZone}";
            }
        }

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