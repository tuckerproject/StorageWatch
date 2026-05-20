using System.Diagnostics;
using System.IO.Compression;
using System.Security.Cryptography;
using StorageWatch.Shared.Update.Models;

namespace StorageWatch.Updater;

/// <summary>
/// Manages updater self-update process.
/// Downloads new updater ZIP, extracts to temp, replaces current updater folder, and restarts.
/// </summary>
internal class SelfUpdateManager
{
    private readonly string _currentUpdaterFolder;
    private readonly string _updaterExePath;

    public SelfUpdateManager()
    {
        _updaterExePath = Environment.ProcessPath
            ?? throw new InvalidOperationException("Could not determine updater executable path.");
        _currentUpdaterFolder = Path.GetDirectoryName(_updaterExePath)
            ?? throw new InvalidOperationException("Could not determine updater folder.");
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

    /// <summary>
    /// Downloads updater ZIP, extracts to staging, and initiates self-replacement.
    /// </summary>
    public async Task<bool> UpdateSelfAsync(
        ComponentUpdateInfo updaterManifestEntry,
        CancellationToken cancellationToken = default)
    {
        // 1. Download updater ZIP
        var tempDir = Path.Combine(Path.GetTempPath(), "StorageWatchUpdaterSelfUpdate", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            var zipPath = Path.Combine(tempDir, "updater.zip");
            using var httpClient = new HttpClient();
            var zipBytes = await httpClient.GetByteArrayAsync(updaterManifestEntry.DownloadUrl, cancellationToken);

            // Verify hash
            using var sha256 = SHA256.Create();
            var downloadedHash = Convert.ToHexString(sha256.ComputeHash(zipBytes)).ToLowerInvariant();
            if (downloadedHash != updaterManifestEntry.Sha256.ToLowerInvariant())
            {
                throw new InvalidOperationException("Updater ZIP hash mismatch.");
            }

            await File.WriteAllBytesAsync(zipPath, zipBytes, cancellationToken);

            // 2. Extract to staging folder
            var stagingFolder = Path.Combine(tempDir, "staging");
            ZipFile.ExtractToDirectory(zipPath, stagingFolder, overwriteFiles: true);

            // 3. Launch replacement script and exit
            LaunchReplacementScript(stagingFolder, _currentUpdaterFolder);
            return true;
        }
        catch
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
            throw;
        }
    }

    private void LaunchReplacementScript(string stagingFolder, string targetFolder)
    {
        // Create PowerShell script to replace updater folder after this process exits
        var scriptPath = Path.Combine(Path.GetTempPath(), $"updater-self-replace-{Guid.NewGuid():N}.ps1");
        var scriptContent = $@"
$ErrorActionPreference = 'Stop'
$currentPid = {Environment.ProcessId}

# Wait for updater process to exit
Wait-Process -Id $currentPid -Timeout 30 -ErrorAction SilentlyContinue

# Replace updater folder
Remove-Item -LiteralPath '{targetFolder}' -Recurse -Force
Copy-Item -LiteralPath '{stagingFolder}' -Destination '{targetFolder}' -Recurse -Force

# Clean up
Remove-Item -LiteralPath '{Path.GetDirectoryName(stagingFolder)}' -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item -LiteralPath '{scriptPath}' -Force -ErrorAction SilentlyContinue
";

        File.WriteAllText(scriptPath, scriptContent);

        // Launch replacement script
        Process.Start(new ProcessStartInfo
        {
            FileName = "powershell.exe",
            Arguments = $"-NoProfile -NonInteractive -ExecutionPolicy Bypass -File \"{scriptPath}\"",
            UseShellExecute = false,
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden
        });

        // Exit updater immediately so script can proceed
        Environment.Exit(0);
    }

    private Version GetCurrentUpdaterVersion()
    {
        var versionInfo = FileVersionInfo.GetVersionInfo(_updaterExePath);
        return Version.Parse(versionInfo.FileVersion ?? "0.0.0.0");
    }
}
