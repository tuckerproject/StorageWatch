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
using StorageWatch.Services.Logging;

namespace StorageWatch.Services.Alerting
{
    /// <summary>
    /// Sends alert messages using the GroupMe Bot API.
    /// </summary>
    public class GroupMeAlertSender : IAlertSender
    {
        private readonly GroupMeOptions _options;
        private readonly HttpClient _httpClient = new();
        private readonly RollingFileLogger _logger;

        public GroupMeAlertSender(GroupMeOptions options, RollingFileLogger logger)
        {
            _options = options;
            _logger = logger;
        }

        public async Task SendAlertAsync(string message)
        {
            if (!_options.Enabled)
            {
                _logger.Log("[GROUPME] Skipping send: GroupMe is disabled in config.");
                return;
            }

            var payload = new
            {
                bot_id = _options.BotId,
                text = message
            };

            var json = JsonSerializer.Serialize(payload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            try
            {
                _logger.Log("[GROUPME] Sending alert via GroupMe Bot API.");
                var response = await _httpClient.PostAsync("https://api.groupme.com/v3/bots/post", content);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.Log($"[GROUPME ERROR] Non-success status code: {(int)response.StatusCode} {response.ReasonPhrase}");
                }
            }
            catch (Exception ex)
            {
                _logger.Log($"[GROUPME ERROR] Exception while sending alert: {ex}");
            }
        }
    }
}