namespace StorageWatchServer.Models
{
    public class UpdateManifest
    {
        public string Version { get; set; } = string.Empty;

        public ComponentUpdateInfo? Service { get; set; }

        public ComponentUpdateInfo? Server { get; set; }

        public ComponentUpdateInfo? Ui { get; set; }

        public List<PluginUpdateInfo> Plugins { get; set; } = new();
    }

    public class ComponentUpdateInfo
    {
        public string Version { get; set; } = string.Empty;

        public string DownloadUrl { get; set; } = string.Empty;

        public string Sha256 { get; set; } = string.Empty;
    }

    public class PluginUpdateInfo
    {
        public string Name { get; set; } = string.Empty;

        public string Version { get; set; } = string.Empty;

        public string DownloadUrl { get; set; } = string.Empty;

        public string Sha256 { get; set; } = string.Empty;
    }
}
