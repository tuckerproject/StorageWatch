/// <summary>
/// Strongly Typed Configuration Options Classes
/// 
/// These classes represent configuration sections using the Options pattern
/// with DataAnnotations validation. They bind directly from JSON configuration.
/// </summary>

using System.ComponentModel.DataAnnotations;

namespace StorageWatch.Config.Options
{
    /// <summary>
    /// Root configuration options. All sections are initialized as new instances to ensure nullability safety.
    /// </summary>
    public class StorageWatchOptions
    {
        /// <summary>
        /// Configuration section key in appsettings.json
        /// </summary>
        public const string SectionKey = "StorageWatch";

        /// <summary>
        /// General service settings section.
        /// </summary>
        [Required]
        public GeneralOptions General { get; set; } = new();

        /// <summary>
        /// Disk monitoring settings section.
        /// </summary>
        [Required]
        public MonitoringOptions Monitoring { get; set; } = new();

        /// <summary>
        /// Database configuration section.
        /// </summary>
        [Required]
        public DatabaseOptions Database { get; set; } = new();

        /// <summary>
        /// Alert delivery settings section (SMTP, GroupMe, etc.)
        /// </summary>
        [Required]
        public AlertingOptions Alerting { get; set; } = new();

        /// <summary>
        /// Central server configuration section.
        /// </summary>
        [Required]
        public CentralServerOptions CentralServer { get; set; } = new();

        /// <summary>
        /// Data retention and cleanup configuration section.
        /// </summary>
        [Required]
        public RetentionOptions Retention { get; set; } = new();
    }

    /// <summary>
    /// General service settings options.
    /// </summary>
    public class GeneralOptions
    {
        /// <summary>
        /// Configuration section key within StorageWatch section
        /// </summary>
        public const string SectionKey = "General";

        /// <summary>
        /// Enables or disables startup diagnostic logging.
        /// </summary>
        public bool EnableStartupLogging { get; set; } = false;
    }

    /// <summary>
    /// Disk monitoring settings options.
    /// </summary>
    public class MonitoringOptions
    {
        /// <summary>
        /// Configuration section key within StorageWatch section
        /// </summary>
        public const string SectionKey = "Monitoring";

        /// <summary>
        /// Disk free space threshold percentage. Alerts trigger when below this value.
        /// Valid range: 1-100
        /// </summary>
        [Range(1, 100, ErrorMessage = "ThresholdPercent must be between 1 and 100")]
        public int ThresholdPercent { get; set; } = 10;

        /// <summary>
        /// List of drive letters to monitor (e.g., ["C:", "D:", "E:"])
        /// </summary>
        [Required]
        [MinLength(1, ErrorMessage = "At least one drive must be specified")]
        public List<string> Drives { get; set; } = new() { "C:" };
    }

    /// <summary>
    /// Database configuration options.
    /// </summary>
    public class DatabaseOptions
    {
        /// <summary>
        /// Configuration section key within StorageWatch section
        /// </summary>
        public const string SectionKey = "Database";

        /// <summary>
        /// SQLite connection string for local data storage.
        /// Example: "Data Source=StorageWatch.db;Version=3;"
        /// </summary>
        [Required]
        [StringLength(500, ErrorMessage = "ConnectionString cannot exceed 500 characters")]
        public string ConnectionString { get; set; } = "Data Source=StorageWatch.db;Version=3;";
    }

    /// <summary>
    /// Alert delivery settings options (SMTP, GroupMe, etc.)
    /// </summary>
    public class AlertingOptions
    {
        /// <summary>
        /// Configuration section key within StorageWatch section
        /// </summary>
        public const string SectionKey = "Alerting";

        /// <summary>
        /// Enables or disables alert notifications.
        /// </summary>
        public bool EnableNotifications { get; set; } = true;

        /// <summary>
        /// Plugin-specific configuration. Each plugin is identified by its PluginId.
        /// Example structure:
        /// "Plugins": {
        ///   "SMTP": { "Enabled": true, "Host": "smtp.example.com", ... },
        ///   "GroupMe": { "Enabled": true, "BotId": "..." },
        ///   "Slack": { "Enabled": false, "WebhookUrl": "..." }
        /// }
        /// </summary>
        public Dictionary<string, Dictionary<string, object>> Plugins { get; set; } = new();

        /// <summary>
        /// SMTP email alert delivery settings (legacy - maintained for backward compatibility).
        /// Prefer using Plugins["SMTP"] for new configurations.
        /// </summary>
        [Required]
        public SmtpOptions Smtp { get; set; } = new();

        /// <summary>
        /// GroupMe bot alert delivery settings (legacy - maintained for backward compatibility).
        /// Prefer using Plugins["GroupMe"] for new configurations.
        /// </summary>
        [Required]
        public GroupMeOptions GroupMe { get; set; } = new();
    }

    /// <summary>
    /// SMTP email alert delivery options.
    /// </summary>
    public class SmtpOptions
    {
        /// <summary>
        /// Configuration section key within Alerting section
        /// </summary>
        public const string SectionKey = "Smtp";

        /// <summary>
        /// Enables or disables SMTP notifications.
        /// </summary>
        public bool Enabled { get; set; } = false;

        /// <summary>
        /// SMTP server hostname.
        /// </summary>
        [StringLength(255)]
        public string Host { get; set; } = string.Empty;

        /// <summary>
        /// SMTP server port number. Valid range: 1-65535
        /// </summary>
        [Range(1, 65535, ErrorMessage = "Port must be between 1 and 65535")]
        public int Port { get; set; } = 587;

        /// <summary>
        /// Whether to use SSL/TLS for SMTP connections.
        /// </summary>
        public bool UseSsl { get; set; } = true;

        /// <summary>
        /// Username for SMTP authentication. Can be encrypted in configuration.
        /// </summary>
        [StringLength(255)]
        public string Username { get; set; } = string.Empty;

        /// <summary>
        /// Password for SMTP authentication. Can be encrypted in configuration.
        /// </summary>
        [StringLength(255)]
        public string Password { get; set; } = string.Empty;

        /// <summary>
        /// Email address to send alerts from.
        /// </summary>
        [EmailAddress]
        public string FromAddress { get; set; } = string.Empty;

        /// <summary>
        /// Email address to send alerts to.
        /// </summary>
        [EmailAddress]
        public string ToAddress { get; set; } = string.Empty;
    }

    /// <summary>
    /// GroupMe bot alert delivery options.
    /// </summary>
    public class GroupMeOptions
    {
        /// <summary>
        /// Configuration section key within Alerting section
        /// </summary>
        public const string SectionKey = "GroupMe";

        /// <summary>
        /// Enables or disables GroupMe notifications.
        /// </summary>
        public bool Enabled { get; set; } = false;

        /// <summary>
        /// GroupMe bot ID for sending messages. Can be encrypted in configuration.
        /// </summary>
        [StringLength(255)]
        public string BotId { get; set; } = string.Empty;
    }

    /// <summary>
    /// Central server aggregation options for multi-machine monitoring.
    /// </summary>
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
        /// [Agent Mode] Optional API key for authentication with central server. Can be encrypted.
        /// Sent as "X-API-Key" header in requests.
        /// </summary>
        [StringLength(255)]
        public string ApiKey { get; set; } = string.Empty;

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

    /// <summary>
    /// SQL reporting options (legacy section for future migration features)
    /// </summary>
    public class SqlReportingOptions
    {
        /// <summary>
        /// Configuration section key within StorageWatch section
        /// </summary>
        public const string SectionKey = "SqlReporting";

        /// <summary>
        /// Enables or disables SQL reporting of disk metrics.
        /// </summary>
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// Executes missed SQL collection on next run if a collection was skipped.
        /// </summary>
        public bool RunMissedCollection { get; set; } = true;

        /// <summary>
        /// Limits SQL collection to at most once per day.
        /// </summary>
        public bool RunOnlyOncePerDay { get; set; } = true;

        /// <summary>
        /// Time of day to execute SQL collection (e.g., "02:00" for 2 AM)
        /// </summary>
        [Required]
        [RegularExpression(@"^\d{2}:\d{2}$", ErrorMessage = "CollectionTime must be in HH:mm format")]
        public string CollectionTime { get; set; } = "02:00";
    }

    /// <summary>
    /// Data retention and cleanup options for SQLite logs.
    /// </summary>
    public class RetentionOptions
    {
        /// <summary>
        /// Configuration section key within StorageWatch section
        /// </summary>
        public const string SectionKey = "Retention";

        /// <summary>
        /// Enables or disables automatic data retention and cleanup.
        /// </summary>
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// Maximum number of days to retain log data. Older entries are deleted.
        /// Valid range: 1-36500 (up to ~100 years)
        /// </summary>
        [Range(1, 36500, ErrorMessage = "MaxDays must be between 1 and 36500")]
        public int MaxDays { get; set; } = 365;

        /// <summary>
        /// Optional maximum number of rows to keep in the DiskSpaceLog table.
        /// When the table exceeds this count, oldest rows are deleted until the target is reached.
        /// If 0 or negative, this constraint is ignored. Default: 0 (no row limit)
        /// </summary>
        [Range(0, int.MaxValue, ErrorMessage = "MaxRows must be non-negative")]
        public int MaxRows { get; set; } = 0;

        /// <summary>
        /// Interval in minutes between automatic cleanup operations.
        /// Valid range: 1-10080 (1 minute to 7 days)
        /// </summary>
        [Range(1, 10080, ErrorMessage = "CleanupIntervalMinutes must be between 1 and 10080")]
        public int CleanupIntervalMinutes { get; set; } = 60;

        /// <summary>
        /// Enables or disables archiving of deleted rows to CSV files before deletion.
        /// </summary>
        public bool ArchiveEnabled { get; set; } = false;

        /// <summary>
        /// Directory path where archived CSV files are stored.
        /// Only used if ArchiveEnabled is true.
        /// Example: "C:\\ProgramData\\StorageWatch\\Archives"
        /// </summary>
        [StringLength(500, ErrorMessage = "ArchiveDirectory cannot exceed 500 characters")]
        public string ArchiveDirectory { get; set; } = string.Empty;

        /// <summary>
        /// Enables or disables exporting of archived data to CSV format.
        /// </summary>
        public bool ExportCsvEnabled { get; set; } = true;
    }
}
