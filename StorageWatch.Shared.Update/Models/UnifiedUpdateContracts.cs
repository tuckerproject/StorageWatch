namespace StorageWatch.Shared.Update.Models;

public static class UnifiedUpdateContract
{
    public const int CurrentVersion = 1;
}

public static class UnifiedUpdateCommands
{
    public const string GetUnifiedUpdateStatus = "GetUnifiedUpdateStatus";
    public const string StartUnifiedInstall = "StartUnifiedInstall";
    public const string GetUnifiedInstallProgress = "GetUnifiedInstallProgress";
    public const string GetLastUnifiedInstallResult = "GetLastUnifiedInstallResult";
}

public class UnifiedInstallUpdateRequest
{
    public int ContractVersion { get; set; } = UnifiedUpdateContract.CurrentVersion;
    public bool UpdateAll { get; set; }
    public List<string> Components { get; set; } = new();
    public bool Force { get; set; }
    public string? RequestedBy { get; set; }
}

public class UnifiedUpdateComponentStatus
{
    public string Component { get; set; } = string.Empty;
    public string CurrentVersion { get; set; } = "0.0.0.0";
    public string LatestVersion { get; set; } = "0.0.0.0";
    public string DownloadUrl { get; set; } = string.Empty;
    public string Sha256 { get; set; } = string.Empty;
    public bool UpdateAvailable { get; set; }
    public string? ErrorMessage { get; set; }
}

public class UnifiedUpdateStatusInfo
{
    public int ContractVersion { get; set; } = UnifiedUpdateContract.CurrentVersion;
    public DateTimeOffset CheckedAtUtc { get; set; }
    public bool AnyUpdateAvailable { get; set; }
    public bool IsInstalling { get; set; }
    public string? LastError { get; set; }
    public List<UnifiedUpdateComponentStatus> Components { get; set; } = new();
}

public class UnifiedUpdateProgressInfo
{
    public int ContractVersion { get; set; } = UnifiedUpdateContract.CurrentVersion;
    public string OrchestrationId { get; set; } = string.Empty;
    public string Phase { get; set; } = string.Empty;
    public string Component { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public double ProgressPercent { get; set; }
    public bool IsIndeterminate { get; set; }
}

public class UnifiedUpdateInstallResult
{
    public int ContractVersion { get; set; } = UnifiedUpdateContract.CurrentVersion;
    public string OrchestrationId { get; set; } = string.Empty;
    public bool Success { get; set; }
    public bool RestartRequired { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTimeOffset StartedAtUtc { get; set; }
    public DateTimeOffset CompletedAtUtc { get; set; }
    public List<string> UpdatedComponents { get; set; } = new();
    public List<string> FailedComponents { get; set; } = new();
}
