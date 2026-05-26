using System.Text.Json;

namespace StorageWatch.Communication.Models;

/// <summary>
/// Represents a request from the UI to the service.
/// </summary>
public class ServiceRequest
{
    /// <summary>
    /// The command to execute (e.g., "GetStatus", "GetLogs", "GetConfig").
    /// </summary>
    public string Command { get; set; } = string.Empty;

    /// <summary>
    /// Optional parameters for the command.
    /// </summary>
    public JsonElement? Parameters { get; set; }
}

/// <summary>
/// Represents a response from the service to the UI.
/// </summary>
public class ServiceResponse
{
    /// <summary>
    /// Indicates whether the request was successful.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Error message if the request failed.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Response data (varies by command).
    /// </summary>
    public JsonElement? Data { get; set; }
}

/// <summary>
/// Service status information.
/// </summary>
public class ServiceStatusInfo
{
    /// <summary>
    /// Current state: Running, Stopped, Paused, Starting, Stopping.
    /// </summary>
    public string State { get; set; } = "Unknown";

    /// <summary>
    /// Service uptime.
    /// </summary>
    public TimeSpan Uptime { get; set; }

    /// <summary>
    /// Last execution timestamp (when monitoring last ran).
    /// </summary>
    public DateTime LastExecutionTimestamp { get; set; }

    /// <summary>
    /// Last error message (if any).
    /// </summary>
    public string? LastError { get; set; }
}

/// <summary>
/// Configuration validation result.
/// </summary>
public class ConfigValidationResult
{
    /// <summary>
    /// Is the configuration valid?
    /// </summary>
    public bool IsValid { get; set; }

    /// <summary>
    /// List of validation errors.
    /// </summary>
    public List<string> Errors { get; set; } = new();

    /// <summary>
    /// List of validation warnings.
    /// </summary>
    public List<string> Warnings { get; set; } = new();
}

/// <summary>
/// Plugin status information.
/// </summary>
public class PluginStatusInfo
{
    /// <summary>
    /// Plugin name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Plugin type (e.g., "SMTP", "GroupMe").
    /// </summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// Is the plugin enabled?
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// Plugin health status (e.g., "Healthy", "Error", "Warning").
    /// </summary>
    public string Health { get; set; } = "Unknown";

    /// <summary>
    /// Last error message (if any).
    /// </summary>
    public string? LastError { get; set; }
}

/// <summary>
/// Local data query request.
/// </summary>
public class LocalDataQuery
{
    /// <summary>
    /// Query type: "RecentUsage", "TrendData", "RetentionStats".
    /// </summary>
    public string QueryType { get; set; } = string.Empty;

    /// <summary>
    /// Drive name (optional, for drive-specific queries).
    /// </summary>
    public string? DriveName { get; set; }

    /// <summary>
    /// Number of days to query (for trend data).
    /// </summary>
    public int DaysBack { get; set; } = 7;

    /// <summary>
    /// Maximum number of records to return.
    /// </summary>
    public int Limit { get; set; } = 1000;
}

public static class UnifiedUpdateCommands
{
    public const string GetUnifiedUpdateStatus = "GetUnifiedUpdateStatus";
    public const string InstallUpdate = "InstallUpdate";
}

public class UnifiedInstallUpdateRequest
{
    public bool UpdateAll { get; set; }

    public List<string> Components { get; set; } = new();

    public bool Force { get; set; }
}

public class UnifiedUpdateComponentStatus
{
    public string Component { get; set; } = string.Empty;

    public string CurrentVersion { get; set; } = "0.0.0.0";

    public string LatestVersion { get; set; } = "0.0.0.0";

    public bool UpdateAvailable { get; set; }
}

public class UnifiedUpdateStatusInfo
{
    public DateTimeOffset CheckedAtUtc { get; set; }

    public bool AnyUpdateAvailable { get; set; }

    public bool IsInstalling { get; set; }

    public string? LastError { get; set; }

    public List<UnifiedUpdateComponentStatus> Components { get; set; } = new();
}

public class UnifiedUpdateProgressInfo
{
    public string OrchestrationId { get; set; } = string.Empty;

    public string Phase { get; set; } = string.Empty;

    public string Component { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public double ProgressPercent { get; set; }

    public bool IsIndeterminate { get; set; }
}

public class UnifiedUpdateInstallResult
{
    public string OrchestrationId { get; set; } = string.Empty;

    public bool Success { get; set; }

    public bool RestartRequired { get; set; }

    public string? ErrorMessage { get; set; }

    public List<string> UpdatedComponents { get; set; } = new();

    public List<string> FailedComponents { get; set; } = new();
}
