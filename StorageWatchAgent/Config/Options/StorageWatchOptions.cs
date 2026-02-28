/// <summary>
/// Strongly Typed Configuration Options Classes
/// 
/// These classes represent configuration sections using the Options pattern
/// with DataAnnotations validation. They bind directly from JSON configuration.
/// </summary>

using System.ComponentModel.DataAnnotations;
using System.IO;

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
        /// Operational mode: Standalone, Agent, or Server.
        /// Default: Standalone (simplest, no external dependencies)
        /// Configuration file will override this default for specific deployments
        /// </summary>
        [Required]
        public StorageWatchMode Mode { get; set; } = StorageWatchMode.Standalone;

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
        /// Central Server settings section
        /// </summary>
        [Required]
        public CentralServerOptions CentralServer { get; set; } = new();

        /// <summary>
        /// Data retention and cleanup configuration section.
        /// </summary>
        [Required]
        public RetentionOptions Retention { get; set; } = new();

        /// <summary>
        /// Auto-update configuration section.
        /// </summary>
        [Required]
        public AutoUpdateOptions AutoUpdate { get; set; } = new();
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
        public List<string> Drives { get; set; } = new();
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
        /// Default: C:\ProgramData\StorageWatch\StorageWatch.db
        /// Example: "Data Source=C:\ProgramData\StorageWatch\StorageWatch.db;Version=3;"
        /// </summary>
        [Required]
        [StringLength(500, ErrorMessage = "ConnectionString cannot exceed 500 characters")]
        public string ConnectionString { get; set; } = "Data Source=C:\\ProgramData\\StorageWatch\\StorageWatch.db;Version=3;";
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

    /// <summary>
    /// Auto-update configuration options.
    /// </summary>
    public class AutoUpdateOptions
    {
        /// <summary>
        /// Configuration section key within StorageWatch section
        /// </summary>
        public const string SectionKey = "AutoUpdate";

        /// <summary>
        /// Enables or disables automatic updates for the service.
        /// </summary>
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// URL to the update manifest JSON.
        /// </summary>
        [StringLength(500, ErrorMessage = "ManifestUrl cannot exceed 500 characters")]
        public string ManifestUrl { get; set; } = string.Empty;

        /// <summary>
        /// Interval in minutes between automatic update checks.
        /// Valid range: 1-10080 (1 minute to 7 days)
        /// </summary>
        [Range(1, 10080, ErrorMessage = "CheckIntervalMinutes must be between 1 and 10080")]
        public int CheckIntervalMinutes { get; set; } = 60;
    }

    /// <summary>
    /// Update notification options.
    /// </summary>
    public class UpdateNotificationOptions
    {
        /// <summary>
        /// Enables or disables notifications for available updates.
        /// </summary>
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// Notification delivery method: Email, Slack, etc.
        /// Default: Email
        /// </summary>
        public string DeliveryMethod { get; set; } = "Email";

        /// <summary>
        /// Optional recipient list for notifications.
        /// Comma-separated email addresses or Slack user IDs, etc.
        /// </summary>
        public string RecipientList { get; set; } = string.Empty;
    }

    /// <summary>
    /// Operational modes for StorageWatch service.
    /// </summary>
    public enum StorageWatchMode
    {
        /// <summary>
        /// Standalone mode: Runs independently on a single host.
        /// </summary>
        Standalone,

        /// <summary>
        /// Agent mode: Runs as an agent reporting to a central server.
        /// Default mode.
        /// </summary>
        Agent,

        /// <summary>
        /// Server mode: Runs as a central server managing multiple agents.
        /// </summary>
        Server
    }
}
