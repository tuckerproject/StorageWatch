using System.Diagnostics;
using System.Net.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StorageWatch.Config.Options;
using StorageWatch.Shared.Update.Models;

namespace StorageWatch.Services.AutoUpdate;

public interface IUnifiedUpdateSnapshotStore
{
    UnifiedUpdateStatusInfo GetSnapshot();
    void SaveSnapshot(UnifiedUpdateStatusInfo snapshot);
}

public class InMemoryUnifiedUpdateSnapshotStore : IUnifiedUpdateSnapshotStore
{
    private readonly object _gate = new();
    private UnifiedUpdateStatusInfo _snapshot = new()
    {
        CheckedAtUtc = DateTimeOffset.MinValue,
        AnyUpdateAvailable = false,
        IsInstalling = false,
        Components =
        {
            new UnifiedUpdateComponentStatus { Component = "agent" },
            new UnifiedUpdateComponentStatus { Component = "server" },
            new UnifiedUpdateComponentStatus { Component = "ui" },
            new UnifiedUpdateComponentStatus { Component = "updater" }
        }
    };

    public UnifiedUpdateStatusInfo GetSnapshot()
    {
        lock (_gate)
        {
            return Clone(_snapshot);
        }
    }

    public void SaveSnapshot(UnifiedUpdateStatusInfo snapshot)
    {
        lock (_gate)
        {
            _snapshot = Clone(snapshot);
        }
    }

    private static UnifiedUpdateStatusInfo Clone(UnifiedUpdateStatusInfo source)
    {
        return new UnifiedUpdateStatusInfo
        {
            ContractVersion = source.ContractVersion,
            CheckedAtUtc = source.CheckedAtUtc,
            AnyUpdateAvailable = source.AnyUpdateAvailable,
            IsInstalling = source.IsInstalling,
            LastError = source.LastError,
            Components = source.Components
                .Select(component => new UnifiedUpdateComponentStatus
                {
                    Component = component.Component,
                    CurrentVersion = component.CurrentVersion,
                    LatestVersion = component.LatestVersion,
                    UpdateAvailable = component.UpdateAvailable,
                    ErrorMessage = component.ErrorMessage
                })
                .ToList()
        };
    }
}

public interface IUnifiedUpdateChecker
{
    Task<UnifiedUpdateStatusInfo> RefreshSnapshotAsync(CancellationToken cancellationToken);
    UnifiedUpdateStatusInfo GetLatestSnapshot();
}

public class UnifiedUpdateChecker : IUnifiedUpdateChecker
{
    private readonly HttpClient _httpClient;
    private readonly IOptions<AutoUpdateOptions> _options;
    private readonly IUnifiedUpdateSnapshotStore _snapshotStore;
    private readonly ILogger<UnifiedUpdateChecker> _logger;

    public UnifiedUpdateChecker(
        HttpClient httpClient,
        IOptions<AutoUpdateOptions> options,
        IUnifiedUpdateSnapshotStore snapshotStore,
        ILogger<UnifiedUpdateChecker> logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _snapshotStore = snapshotStore ?? throw new ArgumentNullException(nameof(snapshotStore));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public UnifiedUpdateStatusInfo GetLatestSnapshot()
    {
        return _snapshotStore.GetSnapshot();
    }

    public async Task<UnifiedUpdateStatusInfo> RefreshSnapshotAsync(CancellationToken cancellationToken)
    {
        var options = _options.Value;
        var checkedAtUtc = DateTimeOffset.UtcNow;

        if (string.IsNullOrWhiteSpace(options.ManifestUrl))
        {
            var missingManifest = CreateFallbackSnapshot(checkedAtUtc, "ManifestUrl is not configured.");
            _snapshotStore.SaveSnapshot(missingManifest);
            return missingManifest;
        }

        try
        {
            var response = await _httpClient.GetAsync(options.ManifestUrl, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                var failedStatus = CreateFallbackSnapshot(checkedAtUtc, $"Manifest request failed with status {(int)response.StatusCode}.");
                _snapshotStore.SaveSnapshot(failedStatus);
                return failedStatus;
            }

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            var manifest = ServiceUpdateChecker.ParseManifest(json);
            if (manifest == null)
            {
                var parseFailed = CreateFallbackSnapshot(checkedAtUtc, "Manifest could not be parsed.");
                _snapshotStore.SaveSnapshot(parseFailed);
                return parseFailed;
            }

            var snapshot = new UnifiedUpdateStatusInfo
            {
                CheckedAtUtc = checkedAtUtc,
                LastError = null,
                IsInstalling = _snapshotStore.GetSnapshot().IsInstalling,
                Components =
                {
                    BuildComponentStatus("agent", "StorageWatchAgent.exe", manifest.Agent),
                    BuildComponentStatus("server", "StorageWatchServer.exe", manifest.Server),
                    BuildComponentStatus("ui", "StorageWatchUI.exe", manifest.Ui),
                    BuildComponentStatus("updater", "StorageWatch.Updater.exe", manifest.Updater)
                }
            };

            snapshot.AnyUpdateAvailable = snapshot.Components.Any(component => component.UpdateAvailable);

            _snapshotStore.SaveSnapshot(snapshot);
            return snapshot;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[AUTOUPDATE] Unified update snapshot refresh failed.");
            var failed = CreateFallbackSnapshot(checkedAtUtc, ex.Message);
            _snapshotStore.SaveSnapshot(failed);
            return failed;
        }
    }

    private static UnifiedUpdateStatusInfo CreateFallbackSnapshot(DateTimeOffset checkedAtUtc, string error)
    {
        return new UnifiedUpdateStatusInfo
        {
            CheckedAtUtc = checkedAtUtc,
            LastError = error,
            AnyUpdateAvailable = false,
            Components =
            {
                BuildComponentStatus("agent", "StorageWatchAgent.exe", latestInfo: null),
                BuildComponentStatus("server", "StorageWatchServer.exe", latestInfo: null),
                BuildComponentStatus("ui", "StorageWatchUI.exe", latestInfo: null),
                BuildComponentStatus("updater", "StorageWatch.Updater.exe", latestInfo: null)
            }
        };
    }

    private static UnifiedUpdateComponentStatus BuildComponentStatus(string component, string executableName, ComponentUpdateInfo? latestInfo)
    {
        var currentVersion = GetInstalledComponentVersion(executableName);
        var normalizedLatest = string.IsNullOrWhiteSpace(latestInfo?.Version) ? "0.0.0.0" : latestInfo.Version;

        var updateAvailable = Version.TryParse(currentVersion, out var current)
            && Version.TryParse(normalizedLatest, out var latest)
            && latest > current;

        return new UnifiedUpdateComponentStatus
        {
            Component = component,
            CurrentVersion = currentVersion,
            LatestVersion = normalizedLatest,
            DownloadUrl = latestInfo?.DownloadUrl ?? string.Empty,
            Sha256 = latestInfo?.Sha256 ?? string.Empty,
            UpdateAvailable = updateAvailable,
            ErrorMessage = null
        };
    }

    private static string GetInstalledComponentVersion(string executableName)
    {
        try
        {
            var baseDir = AppContext.BaseDirectory;
            var installRoot = Path.GetFullPath(Path.Combine(baseDir, ".."));
            var programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
            var candidates = new[]
            {
                Path.Combine(baseDir, executableName),
                Path.Combine(baseDir, "..", executableName),
                Path.Combine(baseDir, "..", "..", executableName),
                Path.Combine(installRoot, "Agent", executableName),
                Path.Combine(installRoot, "Server", executableName),
                Path.Combine(installRoot, "UI", executableName),
                Path.Combine(installRoot, "Updater", executableName),
                Path.Combine(programData, "StorageWatch", "Agent", executableName),
                Path.Combine(programData, "StorageWatch", "Server", executableName),
                Path.Combine(programData, "StorageWatch", "UI", executableName),
                Path.Combine(programData, "StorageWatch", "Updater", executableName)
            };

            foreach (var candidate in candidates)
            {
                var fullPath = Path.GetFullPath(candidate);
                if (!File.Exists(fullPath))
                    continue;

                var version = FileVersionInfo.GetVersionInfo(fullPath).FileVersion;
                if (!string.IsNullOrWhiteSpace(version))
                    return version;
            }
        }
        catch
        {
        }

        return "0.0.0.0";
    }
}