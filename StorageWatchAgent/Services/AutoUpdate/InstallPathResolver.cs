using Microsoft.Win32;

namespace StorageWatch.Services.AutoUpdate;

public class ResolvedInstallPaths
{
    public string InstallRoot { get; set; } = string.Empty;
    public string AgentDirectory { get; set; } = string.Empty;
    public string ServerDirectory { get; set; } = string.Empty;
    public string UiDirectory { get; set; } = string.Empty;
    public string UpdaterDirectory { get; set; } = string.Empty;
    public string UpdaterExecutablePath { get; set; } = string.Empty;
    public bool UsedRegistryInstallRoot { get; set; }
}

public interface IInstallPathResolver
{
    ResolvedInstallPaths Resolve();
}

public class InstallPathResolver : IInstallPathResolver
{
    private const string RegistryKeyPath = @"Software\StorageWatch";
    private const string RegistryInstallDirValue = "InstallDir";

    public ResolvedInstallPaths Resolve()
    {
        var candidateRoots = new List<(string Root, bool IsRegistry)>();

        var registryInstallDir = TryGetRegistryInstallDir();
        if (!string.IsNullOrWhiteSpace(registryInstallDir))
        {
            candidateRoots.Add((Path.GetFullPath(registryInstallDir), true));
        }

        var baseDir = Path.GetFullPath(AppContext.BaseDirectory);
        candidateRoots.Add((Path.GetFullPath(Path.Combine(baseDir, "..")), false));

        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        if (!string.IsNullOrWhiteSpace(programFiles))
        {
            candidateRoots.Add((Path.Combine(programFiles, "StorageWatch"), false));
        }

        foreach (var (root, isRegistry) in candidateRoots.DistinctBy(c => c.Root))
        {
            var resolved = BuildResolvedPaths(root, isRegistry);
            if (IsValid(resolved))
            {
                return resolved;
            }
        }

        var fallbackRoot = Path.GetFullPath(Path.Combine(baseDir, ".."));
        return BuildResolvedPaths(fallbackRoot, false);
    }

    private static ResolvedInstallPaths BuildResolvedPaths(string root, bool isRegistry)
    {
        var installRoot = Path.GetFullPath(root);
        var updaterDirectory = Path.Combine(installRoot, "Updater");

        return new ResolvedInstallPaths
        {
            InstallRoot = installRoot,
            AgentDirectory = Path.Combine(installRoot, "Agent"),
            ServerDirectory = Path.Combine(installRoot, "Server"),
            UiDirectory = Path.Combine(installRoot, "UI"),
            UpdaterDirectory = updaterDirectory,
            UpdaterExecutablePath = Path.Combine(updaterDirectory, "StorageWatch.Updater.exe"),
            UsedRegistryInstallRoot = isRegistry
        };
    }

    private static bool IsValid(ResolvedInstallPaths paths)
    {
        if (string.IsNullOrWhiteSpace(paths.InstallRoot))
            return false;

        if (!Directory.Exists(paths.InstallRoot))
            return false;

        return File.Exists(paths.UpdaterExecutablePath)
               || Directory.Exists(paths.AgentDirectory)
               || Directory.Exists(paths.ServerDirectory)
               || Directory.Exists(paths.UiDirectory);
    }

    private static string? TryGetRegistryInstallDir()
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(RegistryKeyPath, writable: false);
            if (key == null)
                return null;

            var raw = key.GetValue(RegistryInstallDirValue) as string;
            if (string.IsNullOrWhiteSpace(raw))
                return null;

            return raw;
        }
        catch
        {
            return null;
        }
    }
}