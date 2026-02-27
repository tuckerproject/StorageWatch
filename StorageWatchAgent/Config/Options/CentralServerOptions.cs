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
        /// Enables or disables central server functionality.
        /// </summary>
        public bool Enabled { get; set; } = false;

        /// <summary>
        /// Role for this instance: "Agent" (reports to server) or "Server" (aggregates data)
        /// Valid values: "Agent", "Server"
        /// </summary>
        [Required]
        [RegularExpression("^(Agent|Server)$", ErrorMessage = "Mode must be either 'Agent' or 'Server'")]
        public string Mode { get; set; } = "Agent";

        /// <summary>
        /// [Agent Mode] URL of the central server to forward data to.
        /// Example: "http://central-server.example.com:5000"
        /// </summary>
        [Url]
        public string ServerUrl { get; set; } = string.Empty;

        /// <summary>
        /// Configuration alias for ServerUrl.
        /// </summary>
        public string BaseUrl
        {
            get => ServerUrl;
            set => ServerUrl = value;
        }

        /// <summary>
        /// [Agent Mode] Optional API key for authentication with central server. Can be encrypted.
        /// Sent as "X-API-Key" header in requests.
        /// </summary>
        [StringLength(255)]
        public string ApiKey { get; set; } = string.Empty;

        /// <summary>
        /// [Agent Mode] Optional agent identifier to report to the server. Defaults to machine name when empty.
        /// </summary>
        [StringLength(255)]
        public string AgentId { get; set; } = string.Empty;

        /// <summary>
        /// [Agent Mode] Reporting interval in seconds.
        /// </summary>
        [Range(1, 86400, ErrorMessage = "ReportIntervalSeconds must be between 1 and 86400")]
        public int ReportIntervalSeconds { get; set; } = 300;

        /// <summary>
        /// Configuration alias for ReportIntervalSeconds.
        /// </summary>
        public int ReportingIntervalSeconds
        {
            get => ReportIntervalSeconds;
            set => ReportIntervalSeconds = value;
        }

        /// <summary>
        /// [Server Mode] Local port for the central server API. Valid range: 1-65535
        /// Example: 5000 listens on http://localhost:5000
        /// </summary>
        [Range(1, 65535, ErrorMessage = "Port must be between 1 and 65535")]
        public int Port { get; set; } = 5000;

        /// <summary>
        /// [Server Mode] SQLite connection string for central database (separate from agent database)
        /// Example: "Data Source=StorageWatch_Central.db;Version=3;"
        /// </summary>
        [StringLength(500)]
        public string CentralConnectionString { get; set; } = string.Empty;

        /// <summary>
        /// [Server Mode] Optional identifier for this central server instance.
        /// Used for logging and identification.
        /// </summary>
        [StringLength(255)]
        public string ServerId { get; set; } = "central-server";
    }
}
