using StorageWatch.Shared.Update.Models;
using System;
using System.Collections.Generic;

namespace StorageWatch.Services.AutoUpdate
{
    public class ComponentUpdateCheckResult
    {
        public bool IsUpdateAvailable { get; set; }
        public ComponentUpdateInfo? Component { get; set; }
        public string? ErrorMessage { get; set; }
    }

    public class PluginUpdateCheckResult
    {
        public IReadOnlyList<PluginUpdateInfo> Updates { get; set; } = Array.Empty<PluginUpdateInfo>();
        public string? ErrorMessage { get; set; }
    }

    public class UpdateDownloadResult
    {
        public bool Success { get; set; }
        public string? FilePath { get; set; }
        public string? ErrorMessage { get; set; }
    }

    public class PluginDownloadResult : UpdateDownloadResult
    {
        public PluginUpdateInfo? Plugin { get; set; }
    }

    public class UpdateInstallResult
    {
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
    }
}
