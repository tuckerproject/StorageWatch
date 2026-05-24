using System.Diagnostics;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using StorageWatch.Shared.Update.Models;

namespace StorageWatch.Updater;

/// <summary>
/// Manages updater self-update process.
/// Downloads new updater ZIP, extracts to temp, replaces current updater folder, and restarts.
/// </summary>
internal class SelfUpdateManager
{
    private const int ReplaceRetryCount = 15;
    private const int ReplaceRetryDelayMs = 500;

    private readonly string _currentUpdaterFolder;
    private readonly string _updaterExePath;
    private readonly HttpClient _httpClient;
    private readonly IProcessLauncher _processLauncher;
    private readonly Action<int> _sleepAction;

    public SelfUpdateManager()
    {
        _updaterExePath = Environment.ProcessPath
            ?? throw new InvalidOperationException("Could not determine updater executable path.");
        _currentUpdaterFolder = Path.GetDirectoryName(_updaterExePath)
            ?? throw new InvalidOperationException("Could not determine updater folder.");
        _httpClient = new HttpClient();
        _processLauncher = new ProcessLauncher();
        _sleepAction = Thread.Sleep;
    }

    internal SelfUpdateManager(
        string updaterExePath,
        string currentUpdaterFolder,
        HttpClient httpClient,
        IProcessLauncher processLauncher,
        Action<int>? sleepAction = null)
    {
        _updaterExePath = updaterExePath;
        _currentUpdaterFolder = currentUpdaterFolder;
        _httpClient = httpClient;
        _processLauncher = processLauncher;
        _sleepAction = sleepAction ?? Thread.Sleep;
    }

    /// <summary>
    /// Checks if updater self-update is needed by comparing local version to manifest version.
    /// </summary>
    public bool IsUpdateAvailable(ComponentUpdateInfo updaterManifestEntry)
    {
        var currentVersion = GetCurrentUpdaterVersion();
        if (!Version.TryParse(updaterManifestEntry.Version, out var manifestVersion))
        {
            return false;
        }
        return manifestVersion > currentVersion;
    }

    public async Task<bool> RunSelfUpdateStageAsync(
        string manifestPath,
        UpdaterArguments currentArguments,
        CancellationToken cancellationToken = default)
    {
        var manifestJson = await File.ReadAllTextAsync(manifestPath, cancellationToken);
        var manifest = JsonSerializer.Deserialize<UpdateManifest>(manifestJson)
            ?? throw new InvalidOperationException("Manifest could not be parsed.");

        if (manifest.Updater == null)
            throw new InvalidOperationException("Manifest is missing updater component information.");

        return await RunLegacySelfUpdateStageAsync(manifest.Updater, currentArguments, cancellationToken);
    }

    public async Task<bool> RunLegacySelfUpdateStageAsync(
        ComponentUpdateInfo updaterManifestEntry,
        UpdaterArguments currentArguments,
        CancellationToken cancellationToken = default)
    {
        if (!IsUpdateAvailable(updaterManifestEntry))
            return false;

        var tempDir = Path.Combine(Path.GetTempPath(), "StorageWatchUpdaterSelfUpdate", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            var zipPath = Path.Combine(tempDir, "updater.zip");
            var zipBytes = await _httpClient.GetByteArrayAsync(updaterManifestEntry.DownloadUrl, cancellationToken);

            // Verify hash
            using var sha256 = SHA256.Create();
            var downloadedHash = Convert.ToHexString(sha256.ComputeHash(zipBytes)).ToLowerInvariant();
            if (downloadedHash != updaterManifestEntry.Sha256.ToLowerInvariant())
            {
                throw new InvalidOperationException("Updater ZIP hash mismatch.");
            }

            await File.WriteAllBytesAsync(zipPath, zipBytes, cancellationToken);

            var stagingFolder = Path.Combine(tempDir, "staging");
            ZipFile.ExtractToDirectory(zipPath, stagingFolder, overwriteFiles: true);

            var stagedUpdaterExe = Path.Combine(stagingFolder, "StorageWatch.Updater.exe");
            if (!File.Exists(stagedUpdaterExe))
                throw new InvalidOperationException("Staged updater executable was not found in self-update package.");

            var continueArgs = BuildContinuationArguments(currentArguments);
            var encodedContinuation = Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(continueArgs)));

            var applyProcessStarted = _processLauncher.Start(new ProcessStartInfo
            {
                FileName = stagedUpdaterExe,
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = stagingFolder,
                ArgumentList =
                {
                    "--self-update-apply",
                    "--self-update-staging", stagingFolder,
                    "--target", _currentUpdaterFolder,
                    "--continue-args", encodedContinuation
                }
            });

            if (!applyProcessStarted)
                throw new InvalidOperationException("Failed to launch staged updater apply process.");

            return true;
        }
        catch
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
            throw;
        }
    }

    public Task<bool> RunSelfUpdateApplyAsync(UpdaterArguments arguments, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(arguments.SelfUpdateStagingPath))
            throw new InvalidOperationException("Self-update apply requires staging path.");
        if (string.IsNullOrWhiteSpace(arguments.TargetPath))
            throw new InvalidOperationException("Self-update apply requires target path.");

        var stagingFolder = Path.GetFullPath(arguments.SelfUpdateStagingPath);
        var targetFolder = Path.GetFullPath(arguments.TargetPath);

        if (!Directory.Exists(stagingFolder))
            throw new DirectoryNotFoundException($"Self-update staging folder not found: {stagingFolder}");

        ReplaceTargetFolderWithRetries(stagingFolder, targetFolder, cancellationToken, _sleepAction);

        if (!string.IsNullOrWhiteSpace(arguments.ContinueArguments))
        {
            var raw = Encoding.UTF8.GetString(Convert.FromBase64String(arguments.ContinueArguments));
            var continuationArgs = JsonSerializer.Deserialize<string[]>(raw) ?? Array.Empty<string>();
            if (continuationArgs.Length > 0)
            {
                var updatedUpdaterPath = Path.Combine(targetFolder, "StorageWatch.Updater.exe");
                var restart = new ProcessStartInfo
                {
                    FileName = updatedUpdaterPath,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WorkingDirectory = targetFolder
                };

                foreach (var continuationArg in continuationArgs)
                {
                    restart.ArgumentList.Add(continuationArg);
                }

                _processLauncher.Start(restart);
            }
        }

        return Task.FromResult(true);
    }

    internal static string[] BuildContinuationArguments(UpdaterArguments currentArguments)
    {
        if (currentArguments.UpdateUI)
        {
            return BuildUpdateContinuationArguments("--update-ui", "--restart-ui", currentArguments);
        }

        if (currentArguments.UpdateAgent)
        {
            return BuildUpdateContinuationArguments("--update-agent", "--restart-agent", currentArguments);
        }

        if (currentArguments.UpdateServer)
        {
            return BuildUpdateContinuationArguments("--update-server", "--restart-server", currentArguments);
        }

        if (currentArguments.RestartUI)
            return new[] { "--restart-ui" };

        if (currentArguments.RestartAgent)
            return new[] { "--restart-agent" };

        if (currentArguments.RestartServer)
            return new[] { "--restart-server" };

        return Array.Empty<string>();
    }

    private static string[] BuildUpdateContinuationArguments(
        string updateFlag,
        string restartFlag,
        UpdaterArguments currentArguments)
    {
        var args = new List<string> { updateFlag };

        if (!string.IsNullOrWhiteSpace(currentArguments.SourcePath))
        {
            args.Add("--source");
            args.Add(currentArguments.SourcePath);
        }

        if (!string.IsNullOrWhiteSpace(currentArguments.TargetPath))
        {
            args.Add("--target");
            args.Add(currentArguments.TargetPath);
        }

        if (!string.IsNullOrWhiteSpace(currentArguments.ManifestPath))
        {
            args.Add("--manifest");
            args.Add(currentArguments.ManifestPath);
        }

        args.Add(restartFlag);
        return args.ToArray();
    }

    private static void ReplaceTargetFolderWithRetries(string sourceFolder, string targetFolder, CancellationToken cancellationToken, Action<int> sleepAction)
    {
        var targetParent = Path.GetDirectoryName(targetFolder)
            ?? throw new InvalidOperationException("Target updater folder parent could not be determined.");

        Directory.CreateDirectory(targetParent);

        Exception? lastError = null;
        for (var i = 0; i < ReplaceRetryCount; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                if (Directory.Exists(targetFolder))
                {
                    Directory.Delete(targetFolder, recursive: true);
                }

                CopyDirectory(sourceFolder, targetFolder);
                return;
            }
            catch (Exception ex)
            {
                lastError = ex;
                sleepAction(ReplaceRetryDelayMs);
            }
        }

        throw new InvalidOperationException("Failed to apply updater self-update after retries.", lastError);
    }

    private static void CopyDirectory(string sourceDirectory, string destinationDirectory)
    {
        Directory.CreateDirectory(destinationDirectory);

        foreach (var directory in Directory.GetDirectories(sourceDirectory, "*", SearchOption.AllDirectories))
        {
            var relativePath = Path.GetRelativePath(sourceDirectory, directory);
            var destinationSubDir = Path.Combine(destinationDirectory, relativePath);
            Directory.CreateDirectory(destinationSubDir);
        }

        foreach (var file in Directory.GetFiles(sourceDirectory, "*", SearchOption.AllDirectories))
        {
            var relativePath = Path.GetRelativePath(sourceDirectory, file);
            var destinationFile = Path.Combine(destinationDirectory, relativePath);
            var destinationSubDir = Path.GetDirectoryName(destinationFile);
            if (!string.IsNullOrWhiteSpace(destinationSubDir))
                Directory.CreateDirectory(destinationSubDir);

            File.Copy(file, destinationFile, overwrite: true);
        }
    }

    private Version GetCurrentUpdaterVersion()
    {
        var versionInfo = FileVersionInfo.GetVersionInfo(_updaterExePath);
        return Version.Parse(versionInfo.FileVersion ?? "0.0.0.0");
    }
}
