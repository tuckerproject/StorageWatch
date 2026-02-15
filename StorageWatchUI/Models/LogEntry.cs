namespace StorageWatchUI.Models;

/// <summary>
/// Represents a log entry from the service log file.
/// </summary>
public class LogEntry
{
    public DateTime Timestamp { get; set; }
    public string Message { get; set; } = string.Empty;
    public LogLevel Level { get; set; }

    public override string ToString()
    {
        return $"[{Timestamp:yyyy-MM-dd HH:mm:ss}] {Message}";
    }
}

/// <summary>
/// Log entry severity level.
/// </summary>
public enum LogLevel
{
    Info,
    Warning,
    Error
}
