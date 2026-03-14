using System.Text.Json.Serialization;

namespace StorageWatch.Shared.Update.Models;

public class UpdateManifest
{
    public int ManifestVersion { get; set; } = 1;

    [JsonPropertyName("version")]
    public string StorageWatchVersion { get; set; } = "";

    public ComponentUpdateInfo Agent { get; set; } = new();

    public ComponentUpdateInfo Server { get; set; } = new();

    public ComponentUpdateInfo Ui { get; set; } = new();

    public List<PluginUpdateInfo> Plugins { get; set; } = new();

    [JsonIgnore]
    public string Version
    {
        get => StorageWatchVersion;
        set => StorageWatchVersion = value;
    }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonPropertyName("service")]
    public ComponentUpdateInfo? Service
    {
        get => null;
        set
        {
            if (value is not null)
            {
                Agent = value;
            }
        }
    }
}

public class ComponentUpdateInfo
{
    public string Version { get; set; } = "";

    public string DownloadUrl { get; set; } = "";

    public string Sha256 { get; set; } = "";

    public string? ReleaseNotesUrl { get; set; }
}

public class PluginUpdateInfo : ComponentUpdateInfo
{
    public string Id { get; set; } = "";

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonPropertyName("name")]
    public string? Name
    {
        get => null;
        set
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                Id = value;
            }
        }
    }
}
