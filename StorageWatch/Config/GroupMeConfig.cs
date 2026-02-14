/// <summary>
/// GroupMe Configuration Settings
/// 
/// Configuration class for GroupMe alert delivery. Stores the bot ID and enable flag
/// used by the GroupMeAlertSender to send disk space alerts via the GroupMe Bot API.
/// </summary>

namespace StorageWatch.Config
{
    /// <summary>
    /// Configuration settings for GroupMe alert delivery.
    /// </summary>
    public class GroupMeConfig
    {
        /// <summary>
        /// Enables or disables GroupMe notifications.
        /// </summary>
        public bool EnableGroupMe { get; set; }

        /// <summary>
        /// The GroupMe bot ID used to send messages.
        /// </summary>
        public string BotId { get; set; } = string.Empty;
    }
}