/// <summary>
/// Configuration Models for DiskSpaceService
/// 
/// This file defines the main configuration classes that represent all settings
/// for disk monitoring, alerting, and SQL reporting functionality.
/// These classes are populated by the ConfigLoader from the XML configuration file.
/// </summary>

using System;
using System.Collections.Generic;

namespace DiskSpaceService.Config
{
    /// <summary>
    /// Root configuration class containing all settings for the DiskSpaceService application.
    /// Includes SQL reporting, notifications, disk monitoring, and alert delivery configurations.
    /// </summary>
    public class DiskSpaceConfig
    {
        /// <summary>
        /// Enables or disables SQL database reporting of disk space metrics.
        /// When true, disk space data is periodically written to the database.
        /// </summary>
        public bool EnableSqlReporting { get; set; }

        /// <summary>
        /// When true, if the scheduled SQL run was missed, it will be executed as soon as possible.
        /// </summary>
        public bool RunMissedCollection { get; set; }

        /// <summary>
        /// When true, SQL collection will run at most once per day.
        /// When false, SQL collection can run multiple times per day.
        /// </summary>
        public bool RunOnlyOncePerDay { get; set; }

        /// <summary>
        /// The daily time (TimeSpan) at which SQL collection should execute (e.g., "02:00" for 2 AM).
        /// </summary>
        public TimeSpan CollectionTime { get; set; }

        /// <summary>
        /// Enables or disables alert notifications for disk space issues.
        /// When true, alerts are sent via configured delivery methods (GroupMe, SMTP, etc.).
        /// </summary>
        public bool EnableNotifications { get; set; }

        /// <summary>
        /// List of drive letters to monitor for disk space (e.g., ["C:", "D:", "E:"]).
        /// </summary>
        public List<string> Drives { get; set; } = new();

        /// <summary>
        /// The threshold percentage of free space. When a drive's free space falls below this percentage,
        /// an alert is triggered (e.g., 10 means alert when less than 10% free).
        /// </summary>
        public int ThresholdPercent { get; set; }

        /// <summary>
        /// Database configuration containing the connection string for SQL operations.
        /// </summary>
        public DatabaseConfig Database { get; set; } = new();

        /// <summary>
        /// GroupMe alert configuration for sending notifications via GroupMe bot API.
        /// </summary>
        public GroupMeConfig GroupMe { get; set; } = new();

        /// <summary>
        /// SMTP email configuration for sending alert emails.
        /// </summary>
        public SmtpConfig Smtp { get; set; } = new();

        /// <summary>
        /// Enables or disables startup logging messages for diagnostic purposes.
        /// </summary>
        public bool EnableStartupLogging { get; set; }
    }

    /// <summary>
    /// Database connection configuration.
    /// </summary>
    public class DatabaseConfig
    {
        /// <summary>
        /// SQL Server connection string used for reporting and data storage.
        /// Example: "Server=localhost;Database=DiskSpace;Integrated Security=true;"
        /// </summary>
        public string ConnectionString { get; set; } = string.Empty;
    }
}