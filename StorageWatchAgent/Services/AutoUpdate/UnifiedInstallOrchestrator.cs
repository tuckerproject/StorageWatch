using System.Diagnostics;
using System.IO.Compression;
using System.Text.Json;
using StorageWatch.Shared.Update.Models;

namespace StorageWatch.Services.AutoUpdate;

public interface IUnifiedInstallOrchestrator
{
    Task<UnifiedUpdateInstallResult> StartInstallAsync(UnifiedInstallUpdateRequest request, CancellationToken cancellationToken);
    UnifiedUpdateProgressInfo GetProgress();
    UnifiedUpdateInstallResult GetLastResult();
}

public class UnifiedInstallOrchestrator : IUnifiedInstallOrchestrator
{
    private static readonly string[] DefaultAllOrder = ["updater", "server", "ui", "agent"];
    private static readonly TimeSpan UpdaterInvocationTimeout = TimeSpan.FromMinutes(15);

    private readonly IUnifiedUpdateChecker _unifiedUpdateChecker;
    private readonly IServiceUpdateDownloader _serviceUpdateDownloader;
    private readonly IInstallPathResolver _installPathResolver;
    private readonly ILogger<UnifiedInstallOrchestrator> _logger;
    private readonly SemaphoreSlim _orchestrationGate = new(1, 1);
    private readonly object _stateGate = new();
    private UnifiedUpdateProgressInfo _latestProgress = new()
    {
        OrchestrationId = string.Empty,
        Phase = "idle",
        Component = string.Empty,
        Status = "Idle",
        ProgressPercent = 0,
        IsIndeterminate = false
    };
    private UnifiedUpdateInstallResult _lastResult = new()
    {
        OrchestrationId = string.Empty,
        Success = false,
        RestartRequired = false,
        ErrorMessage = null,
        StartedAtUtc = DateTimeOffset.MinValue,
        CompletedAtUtc = DateTimeOffset.MinValue
    };

    public UnifiedInstallOrchestrator(
        IUnifiedUpdateChecker unifiedUpdateChecker,
        IServiceUpdateDownloader serviceUpdateDownloader,
        IInstallPathResolver installPathResolver,
        ILogger<UnifiedInstallOrchestrator> logger)
    {
        _unifiedUpdateChecker = unifiedUpdateChecker ?? throw new ArgumentNullException(nameof(unifiedUpdateChecker));
        _serviceUpdateDownloader = serviceUpdateDownloader ?? throw new ArgumentNullException(nameof(serviceUpdateDownloader));
        _installPathResolver = installPathResolver ?? throw new ArgumentNullException(nameof(installPathResolver));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<UnifiedUpdateInstallResult> StartInstallAsync(UnifiedInstallUpdateRequest request, CancellationToken cancellationToken)
    {
        if (!await _orchestrationGate.WaitAsync(0, cancellationToken))
        {
            return new UnifiedUpdateInstallResult
            {
                OrchestrationId = _latestProgress.OrchestrationId,
                Success = false,
                ErrorMessage = "An update orchestration is already active.",
                StartedAtUtc = DateTimeOffset.UtcNow,
                CompletedAtUtc = DateTimeOffset.UtcNow
            };
        }

        var orchestrationId = Guid.NewGuid().ToString("N");
        var startedAt = DateTimeOffset.UtcNow;

        try
        {
            SetProgress(orchestrationId, "initializing", string.Empty, "Preparing update orchestration...", 0, true);

            var status = await _unifiedUpdateChecker.RefreshSnapshotAsync(cancellationToken);
            var plan = BuildPlan(request, status);

            if (plan.Count == 0)
            {
                var noOp = new UnifiedUpdateInstallResult
                {
                    OrchestrationId = orchestrationId,
                    Success = true,
                    RestartRequired = false,
                    StartedAtUtc = startedAt,
                    CompletedAtUtc = DateTimeOffset.UtcNow
                };

                SetProgress(orchestrationId, "completed", string.Empty, "No applicable updates were available.", 100, false);
                SetLastResult(noOp);
                return noOp;
            }

            var paths = _installPathResolver.Resolve();
            var result = new UnifiedUpdateInstallResult
            {
                OrchestrationId = orchestrationId,
                StartedAtUtc = startedAt,
                RestartRequired = false
            };

            foreach (var component in plan)
            {
                cancellationToken.ThrowIfCancellationRequested();
                SetProgress(orchestrationId, "installing", component, $"Applying update for {component}...", ComputePercent(result.UpdatedComponents.Count, plan.Count), true);
                var componentStatus = status.Components.FirstOrDefault(c => string.Equals(c.Component, component, StringComparison.OrdinalIgnoreCase));
                if (componentStatus == null)
                {
                    result.FailedComponents.Add(component);
                    SetProgress(orchestrationId, "failed", component, $"Missing component metadata for {component}.", ComputePercent(result.UpdatedComponents.Count, plan.Count), false);
                    continue;
                }

                var downloadResult = await _serviceUpdateDownloader.DownloadAsync(new ComponentUpdateInfo
                {
                    Version = componentStatus.LatestVersion,
                    DownloadUrl = componentStatus.DownloadUrl,
                    Sha256 = componentStatus.Sha256
                }, cancellationToken);

                if (!downloadResult.Success || string.IsNullOrWhiteSpace(downloadResult.FilePath))
                {
                    result.FailedComponents.Add(component);
                    result.ErrorMessage = downloadResult.ErrorMessage ?? $"{component} download failed.";
                    SetProgress(orchestrationId, "failed", component, result.ErrorMessage, ComputePercent(result.UpdatedComponents.Count, plan.Count), false);
                    break;
                }

                var invocation = PrepareInvocation(component, componentStatus, downloadResult.FilePath, paths);
                if (!invocation.Success)
                {
                    result.FailedComponents.Add(component);
                    result.ErrorMessage = invocation.Error;
                    SetProgress(orchestrationId, "failed", component, result.ErrorMessage ?? $"Failed to prepare update for {component}.", ComputePercent(result.UpdatedComponents.Count, plan.Count), false);
                    break;
                }

                var run = RunUpdaterProcess(invocation.UpdaterPath, invocation.Arguments, invocation.WorkingDirectory);
                if (!run.Success)
                {
                    result.FailedComponents.Add(component);
                    result.ErrorMessage = run.ErrorMessage ?? $"Failed to launch updater for component '{component}'.";
                    SetProgress(orchestrationId, "failed", component, result.ErrorMessage, ComputePercent(result.UpdatedComponents.Count, plan.Count), false);
                    break;
                }

                result.UpdatedComponents.Add(component);
                SetProgress(orchestrationId, "installing", component, $"Update applied for {component}.", ComputePercent(result.UpdatedComponents.Count, plan.Count), false);
            }

            result.Success = result.FailedComponents.Count == 0;
            result.CompletedAtUtc = DateTimeOffset.UtcNow;
            SetProgress(
                orchestrationId,
                result.Success ? "completed" : "failed",
                string.Empty,
                result.Success ? "Unified update orchestration completed." : (result.ErrorMessage ?? "Unified update orchestration failed."),
                result.Success ? 100 : ComputePercent(result.UpdatedComponents.Count, plan.Count),
                false);
            SetLastResult(result);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[AUTOUPDATE] Unified install orchestration failed.");
            var failed = new UnifiedUpdateInstallResult
            {
                OrchestrationId = orchestrationId,
                Success = false,
                ErrorMessage = ex.Message,
                StartedAtUtc = startedAt,
                CompletedAtUtc = DateTimeOffset.UtcNow
            };

            SetProgress(orchestrationId, "failed", string.Empty, ex.Message, 0, false);
            SetLastResult(failed);
            return failed;
        }
        finally
        {
            _orchestrationGate.Release();
        }
    }

    public UnifiedUpdateProgressInfo GetProgress()
    {
        lock (_stateGate)
        {
            return Clone(_latestProgress);
        }
    }

    public UnifiedUpdateInstallResult GetLastResult()
    {
        lock (_stateGate)
        {
            return Clone(_lastResult);
        }
    }

    private static List<string> BuildPlan(UnifiedInstallUpdateRequest request, UnifiedUpdateStatusInfo status)
    {
        var updateable = status.Components
            .Where(component => component.UpdateAvailable)
            .Select(component => component.Component)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (request.UpdateAll)
        {
            return DefaultAllOrder.Where(updateable.Contains).ToList();
        }

        if (request.Components.Count == 0)
            return new List<string>();

        return request.Components
            .Where(updateable.Contains)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static double ComputePercent(int completed, int total)
    {
        if (total <= 0)
            return 0;

        return Math.Clamp((completed / (double)total) * 100d, 0d, 100d);
    }

    private void SetProgress(string orchestrationId, string phase, string component, string status, double progressPercent, bool isIndeterminate)
    {
        lock (_stateGate)
        {
            _latestProgress = new UnifiedUpdateProgressInfo
            {
                OrchestrationId = orchestrationId,
                Phase = phase,
                Component = component,
                Status = status,
                ProgressPercent = progressPercent,
                IsIndeterminate = isIndeterminate
            };
        }
    }

    private void SetLastResult(UnifiedUpdateInstallResult result)
    {
        lock (_stateGate)
        {
            _lastResult = Clone(result);
        }
    }

    private static UnifiedUpdateProgressInfo Clone(UnifiedUpdateProgressInfo source)
    {
        return new UnifiedUpdateProgressInfo
        {
            ContractVersion = source.ContractVersion,
            OrchestrationId = source.OrchestrationId,
            Phase = source.Phase,
            Component = source.Component,
            Status = source.Status,
            ProgressPercent = source.ProgressPercent,
            IsIndeterminate = source.IsIndeterminate
        };
    }

    private static UnifiedUpdateInstallResult Clone(UnifiedUpdateInstallResult source)
    {
        return new UnifiedUpdateInstallResult
        {
            ContractVersion = source.ContractVersion,
            OrchestrationId = source.OrchestrationId,
            Success = source.Success,
            RestartRequired = source.RestartRequired,
            ErrorMessage = source.ErrorMessage,
            StartedAtUtc = source.StartedAtUtc,
            CompletedAtUtc = source.CompletedAtUtc,
            UpdatedComponents = source.UpdatedComponents.ToList(),
            FailedComponents = source.FailedComponents.ToList()
        };
    }

    private static (bool Success, string UpdaterPath, string WorkingDirectory, List<string> Arguments, string? Error) PrepareInvocation(string component, UnifiedUpdateComponentStatus componentStatus, string zipPath, ResolvedInstallPaths paths)
    {
        if (!File.Exists(paths.UpdaterExecutablePath))
        {
            return (false, string.Empty, string.Empty, new List<string>(), $"Updater executable was not found at '{paths.UpdaterExecutablePath}'.");
        }

        var stagingDirectory = Path.Combine(Path.GetTempPath(), "StorageWatchUnifiedUpdate", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(stagingDirectory);
        if (!string.Equals(component, "updater", StringComparison.OrdinalIgnoreCase))
        {
            ZipFile.ExtractToDirectory(zipPath, stagingDirectory, overwriteFiles: true);
        }

        var manifestPath = EnsureStagingManifest(stagingDirectory, component, componentStatus);
        var targetDirectory = ResolveTargetDirectory(component, paths);
        if (string.IsNullOrWhiteSpace(targetDirectory))
        {
            return (false, string.Empty, string.Empty, new List<string>(), $"Unknown component target: '{component}'.");
        }

        var args = BuildUpdaterArguments(component, stagingDirectory, targetDirectory, manifestPath);
        return (true, paths.UpdaterExecutablePath, Path.GetDirectoryName(paths.UpdaterExecutablePath) ?? AppContext.BaseDirectory, args, null);
    }

    private static (bool Success, string? ErrorMessage) RunUpdaterProcess(string updaterPath, IReadOnlyList<string> args, string workingDirectory)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = updaterPath,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = workingDirectory
        };

        foreach (var arg in args)
        {
            startInfo.ArgumentList.Add(arg);
        }

        var process = Process.Start(startInfo);
        if (process == null)
        {
            return (false, "Updater process could not be started.");
        }

        var exited = process.WaitForExit((int)UpdaterInvocationTimeout.TotalMilliseconds);
        if (!exited)
        {
            try
            {
                process.Kill(entireProcessTree: true);
            }
            catch
            {
            }

            return (false, "Updater process timed out.");
        }

        if (process.ExitCode != 0)
        {
            return (false, $"Updater process failed with exit code {process.ExitCode}.");
        }

        return (true, null);
    }

    private static string ResolveTargetDirectory(string component, ResolvedInstallPaths paths)
    {
        return component.ToLowerInvariant() switch
        {
            "agent" => paths.AgentDirectory,
            "server" => paths.ServerDirectory,
            "ui" => paths.UiDirectory,
            "updater" => paths.UpdaterDirectory,
            _ => string.Empty
        };
    }

    private static List<string> BuildUpdaterArguments(string component, string source, string target, string manifest)
    {
        var updateFlag = component.ToLowerInvariant() switch
        {
            "agent" => "--update-agent",
            "server" => "--update-server",
            "ui" => "--update-ui",
            "updater" => "--self-update-stage",
            _ => string.Empty
        };

        var restartFlag = component.ToLowerInvariant() switch
        {
            "agent" => "--restart-agent",
            "server" => "--restart-server",
            "ui" => "--restart-ui",
            _ => string.Empty
        };

        if (string.Equals(component, "updater", StringComparison.OrdinalIgnoreCase))
        {
            return new List<string> { "--self-update-stage", "--manifest", manifest };
        }

        var args = new List<string> { updateFlag, "--source", source, "--target", target, "--manifest", manifest };
        if (!string.IsNullOrWhiteSpace(restartFlag))
        {
            args.Add(restartFlag);
        }

        return args;
    }

    private static string EnsureStagingManifest(string stagingDirectory, string component, UnifiedUpdateComponentStatus componentStatus)
    {
        var existingManifest = Directory
            .EnumerateFiles(stagingDirectory, "*.json", SearchOption.TopDirectoryOnly)
            .FirstOrDefault(path => string.Equals(Path.GetFileName(path), "manifest.json", StringComparison.OrdinalIgnoreCase));

        if (!string.IsNullOrWhiteSpace(existingManifest))
            return existingManifest;

        var manifestPath = Path.Combine(stagingDirectory, "manifest.json");
        var manifestJson = string.Equals(component, "updater", StringComparison.OrdinalIgnoreCase)
            ? JsonSerializer.Serialize(new
            {
                version = componentStatus.LatestVersion,
                updater = new
                {
                    version = componentStatus.LatestVersion,
                    downloadUrl = componentStatus.DownloadUrl,
                    sha256 = componentStatus.Sha256
                }
            })
            : JsonSerializer.Serialize(new
            {
                component,
                createdUtc = DateTimeOffset.UtcNow
            });
        File.WriteAllText(manifestPath, manifestJson);
        return manifestPath;
    }

}