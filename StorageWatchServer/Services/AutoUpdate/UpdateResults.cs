using StorageWatchServer.Models;

namespace StorageWatchServer.Services.AutoUpdate
{
    public class ComponentUpdateCheckResult
    {
        public bool IsUpdateAvailable { get; set; }
        public ComponentUpdateInfo? Component { get; set; }
        public string? ErrorMessage { get; set; }
    }

    public class UpdateDownloadResult
    {
        public bool Success { get; set; }
        public string? FilePath { get; set; }
        public string? ErrorMessage { get; set; }
    }

    public class UpdateInstallResult
    {
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
    }
}
