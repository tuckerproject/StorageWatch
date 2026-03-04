/// <summary>
/// Central server aggregation options for multi-machine monitoring.
/// </summary>

using System.ComponentModel.DataAnnotations;

namespace StorageWatch.Config.Options
{
    public class CentralServerOptions
    {
        /// <summary>
        /// Configuration section key within StorageWatch section
        /// </summary>
        public const string SectionKey = "CentralServer";

        /// <summary>
        /// URL of the central server to publish data to.
        /// Example: "http://central-server.example.com:5000"
        /// Required for Agent mode.
        /// </summary>
        [Required]
        [Url]
        public string ServerUrl { get; set; } = string.Empty;

        /// <summary>
        /// Interval in seconds to check for new rows and publish to the central server.
        /// Default: 300 seconds (5 minutes)
        /// </summary>
        [Range(1, 86400, ErrorMessage = "CheckIntervalSeconds must be between 1 and 86400")]
        public int CheckIntervalSeconds { get; set; } = 300;

        /// <summary>
        /// Optional API key for authentication with central server. Can be encrypted.
        /// Sent as "X-API-Key" header in requests.
        /// </summary>
        [StringLength(255)]
        public string ApiKey { get; set; } = string.Empty;
    }
}
