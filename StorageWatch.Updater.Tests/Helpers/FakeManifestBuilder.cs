using StorageWatch.Shared.Update.Models;
using System.Text.Json;

namespace StorageWatch.Updater.Tests.Helpers;

public sealed class FakeManifestBuilder
{
    private readonly UpdateManifest _manifest = new()
    {
        StorageWatchVersion = "1.0.0",
        Ui = new ComponentUpdateInfo
        {
            Version = "1.0.0",
            DownloadUrl = "https://updates.test/ui.zip",
            Sha256 = new string('a', 64)
        },
        Agent = new ComponentUpdateInfo
        {
            Version = "1.0.0",
            DownloadUrl = "https://updates.test/agent.zip",
            Sha256 = new string('b', 64)
        },
        Server = new ComponentUpdateInfo
        {
            Version = "1.0.0",
            DownloadUrl = "https://updates.test/server.zip",
            Sha256 = new string('c', 64)
        },
        Updater = new ComponentUpdateInfo
        {
            Version = "1.0.0",
            DownloadUrl = "https://updates.test/updater.zip",
            Sha256 = new string('d', 64)
        }
    };

    public FakeManifestBuilder WithVersion(string version)
    {
        _manifest.StorageWatchVersion = version;
        return this;
    }

    public FakeManifestBuilder WithUpdater(ComponentUpdateInfo info)
    {
        _manifest.Updater = info;
        return this;
    }

    public FakeManifestBuilder WithoutUpdater()
    {
        _manifest.Updater = null!;
        return this;
    }

    public UpdateManifest Build() => _manifest;

    public string BuildJson() => JsonSerializer.Serialize(_manifest);

    public string WriteTo(string path)
    {
        var json = BuildJson();
        File.WriteAllText(path, json);
        return path;
    }
}
