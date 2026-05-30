using System.Diagnostics;
using System.IO.Compression;
using System.Text.Json;
using StorageWatch.Shared.Update.Models;
using StorageWatchAgent.Services.AutoUpdate;
using StorageWatchAgent.Services.AutoUpdate.Models;

namespace StorageWatch.Services.AutoUpdate;

public interface IUnifiedInstallOrchestrator
{
    Task<UnifiedUpdateInstallResult> StartInstallAsync(UnifiedInstallUpdateRequest request, CancellationToken cancellationToken);
    Task<UnifiedUpdateInstallResult> ResumePendingInstallAsync(CancellationToken cancellationToken);
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
    private readonly IUnifiedInstallCheckpointStore _checkpointStore;
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
        IUnifiedInstallCheckpointStore checkpointStore,
        ILogger<UnifiedInstallOrchestrator> logger)
    {
        _unifiedUpdateChecker = unifiedUpdateChecker ?? throw new ArgumentNullException(nameof(unifiedUpdateChecker));
        _serviceUpdateDownloader = serviceUpdateDownloader ?? throw new ArgumentNullException(nameof(serviceUpdateDownloader));
        _installPathResolver = installPathResolver ?? throw new ArgumentNullException(nameof(installPathResolver));
        _checkpointStore = checkpointStore ?? throw new ArgumentNullException(nameof(checkpointStore));
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

            // Initialize checkpoint before starting install
            var checkpoint = CreateCheckpoint(orchestrationId, startedAt, request, plan, status);
            checkpoint.IsInstalling = true;
            await _checkpointStore.SaveCheckpointAsync(checkpoint, cancellationToken);
            await _unifiedUpdateChecker.SetInstallingStateAsync(true, cancellationToken);

            _logger.LogInformation(
                "[AUTOUPDATE] Starting unified install for orchestration {OrchestrationId} with {Count} components: {Components}",
                orchestrationId,
                plan.Count,
                string.Join(", ", plan)
            );

            var result = await ExecuteInstallPlanAsync(orchestrationId, startedAt, checkpoint, cancellationToken);

            // Clear checkpoint on completion
            checkpoint.IsInstalling = false;
            await _checkpointStore.ClearCheckpointAsync(cancellationToken);
            await _unifiedUpdateChecker.SetInstallingStateAsync(false, cancellationToken);

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

            // Clear checkpoint on fatal error
            try
            {
                await _checkpointStore.ClearCheckpointAsync(cancellationToken);
                await _unifiedUpdateChecker.SetInstallingStateAsync(false, cancellationToken);
            }
            catch (Exception clearEx)
            {
                _logger.LogWarning(clearEx, "Failed to clear checkpoint after fatal error");
            }

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

    public async Task<UnifiedUpdateInstallResult> ResumePendingInstallAsync(CancellationToken cancellationToken)
    {
        if (!await _orchestrationGate.WaitAsync(0, cancellationToken))
        {
            _logger.LogWarning("[AUTOUPDATE] Resume rejected: orchestration already active");
            return new UnifiedUpdateInstallResult
            {
                OrchestrationId = string.Empty,
                Success = false,
                ErrorMessage = "Cannot resume: an orchestration is already active.",
                StartedAtUtc = DateTimeOffset.UtcNow,
                CompletedAtUtc = DateTimeOffset.UtcNow
            };
        }

        try
        {
            var checkpoint = await _checkpointStore.LoadCheckpointAsync(cancellationToken);
            if (checkpoint == null || !checkpoint.IsInstalling)
            {
                _logger.LogInformation("[AUTOUPDATE] No pending checkpoint to resume");
                return new UnifiedUpdateInstallResult
                {
                    OrchestrationId = string.Empty,
                    Success = false,
                    ErrorMessage = "No pending install to resume.",
                    StartedAtUtc = DateTimeOffset.UtcNow,
                    CompletedAtUtc = DateTimeOffset.UtcNow
                };
            }

            _logger.LogInformation(
                "[AUTOUPDATE] Resuming orchestration {OrchestrationId} from component index {Index}/{Total}",
                checkpoint.OrchestrationId,
                checkpoint.CurrentComponentIndex,
                checkpoint.Components.Count
            );

            SetProgress(checkpoint.OrchestrationId, "resuming", string.Empty, "Resuming update orchestration...", 0, true);

            var result = await ExecuteInstallPlanAsync(
                checkpoint.OrchestrationId,
                checkpoint.StartedAtUtc,
                checkpoint,
                cancellationToken);

            // Clear checkpoint after resume completes
            checkpoint.IsInstalling = false;
            await _checkpointStore.ClearCheckpointAsync(cancellationToken);
            await _unifiedUpdateChecker.SetInstallingStateAsync(false, cancellationToken);

            SetProgress(
                checkpoint.OrchestrationId,
                result.Success ? "completed" : "failed",
                string.Empty,
                result.Success ? "Unified update orchestration resumed and completed." : (result.ErrorMessage ?? "Resumed orchestration failed."),
                result.Success ? 100 : ComputePercent(result.UpdatedComponents.Count, checkpoint.Components.Count),
                false);
            SetLastResult(result);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[AUTOUPDATE] Resume failed");

            try
            {
                await _checkpointStore.ClearCheckpointAsync(cancellationToken);
                await _unifiedUpdateChecker.SetInstallingStateAsync(false, cancellationToken);
            }
            catch (Exception clearEx)
            {
                _logger.LogWarning(clearEx, "Failed to clear checkpoint after resume error");
            }

            var failed = new UnifiedUpdateInstallResult
            {
                OrchestrationId = string.Empty,
                Success = false,
                ErrorMessage = ex.Message,
                StartedAtUtc = DateTimeOffset.UtcNow,
                CompletedAtUtc = DateTimeOffset.UtcNow
            };
            SetLastResult(failed);
            return failed;
        }
        finally
        {
            _orchestrationGate.Release();
        }
    }

    private async Task<UnifiedUpdateInstallResult> ExecuteInstallPlanAsync(
        string orchestrationId,
        DateTimeOffset startedAt,
        UnifiedInstallCheckpoint checkpoint,
        CancellationToken cancellationToken)
    {
        var paths = _installPathResolver.Resolve();
        var result = new UnifiedUpdateInstallResult
        {
            OrchestrationId = orchestrationId,
            StartedAtUtc = startedAt,
            RestartRequired = false
        };

        // Restore already-completed components from checkpoint
        foreach (var state in checkpoint.ComponentStates.Where(s => s.State == ComponentInstallState.Completed))
        {
            result.UpdatedComponents.Add(state.Component);
        }

        for (int i = checkpoint.CurrentComponentIndex; i < checkpoint.Components.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var component = checkpoint.Components[i];
            var componentState = checkpoint.ComponentStates[i];

            // Skip if already completed (idempotent resume)
            if (componentState.State == ComponentInstallState.Completed)
            {
                _logger.LogDebug(
                    "[AUTOUPDATE] Component {Component} already completed, skipping (index {Index})",
                    component,
                    i
                );
                continue;
            }

            checkpoint.CurrentComponentIndex = i;
            componentState.State = ComponentInstallState.InProgress;
            componentState.UpdatedAtUtc = DateTimeOffset.UtcNow;
            await _checkpointStore.SaveCheckpointAsync(checkpoint, cancellationToken);

            SetProgress(orchestrationId, "installing", component, $"Applying update for {component}...", ComputePercent(result.UpdatedComponents.Count, checkpoint.Components.Count), true);

            _logger.LogInformation(
                "[AUTOUPDATE] Installing component {Component} (index {Index}/{Total}, version {Version})",
                component,
                i,
                checkpoint.Components.Count,
                componentState.TargetVersion
            );

            // Download if not already present
            string? localZipPath = componentState.LocalZipPath;
            if (string.IsNullOrWhiteSpace(localZipPath) || !File.Exists(localZipPath))
            {
                var downloadResult = await _serviceUpdateDownloader.DownloadAsync(new ComponentUpdateInfo
                {
                    Version = componentState.TargetVersion,
                    DownloadUrl = componentState.DownloadUrl,
                    Sha256 = componentState.Sha256
                }, cancellationToken);

                if (!downloadResult.Success || string.IsNullOrWhiteSpace(downloadResult.FilePath))
                {
                    componentState.State = ComponentInstallState.Failed;
                    componentState.ErrorMessage = downloadResult.ErrorMessage ?? $"{component} download failed.";
                    result.FailedComponents.Add(component);
                    result.ErrorMessage = componentState.ErrorMessage;
                    await _checkpointStore.SaveCheckpointAsync(checkpoint, cancellationToken);
                    SetProgress(orchestrationId, "failed", component, result.ErrorMessage, ComputePercent(result.UpdatedComponents.Count, checkpoint.Components.Count), false);
                    break;
                }

                localZipPath = downloadResult.FilePath;
                componentState.LocalZipPath = localZipPath;
                await _checkpointStore.SaveCheckpointAsync(checkpoint, cancellationToken);
            }

            // Prepare invocation
            var componentStatus = new UnifiedUpdateComponentStatus
            {
                Component = component,
                LatestVersion = componentState.TargetVersion,
                DownloadUrl = componentState.DownloadUrl,
                Sha256 = componentState.Sha256
            };

            var invocation = PrepareInvocation(component, componentStatus, localZipPath, paths);
            if (!invocation.Success)
            {
                componentState.State = ComponentInstallState.Failed;
                componentState.ErrorMessage = invocation.Error;
                result.FailedComponents.Add(component);
                result.ErrorMessage = invocation.Error;
                await _checkpointStore.SaveCheckpointAsync(checkpoint, cancellationToken);
                SetProgress(orchestrationId, "failed", component, result.ErrorMessage ?? $"Failed to prepare update for {component}.", ComputePercent(result.UpdatedComponents.Count, checkpoint.Components.Count), false);
                break;
            }

            // Run updater
            var run = RunUpdaterProcess(invocation.UpdaterPath, invocation.Arguments, invocation.WorkingDirectory);
            if (!run.Success)
            {
                componentState.State = ComponentInstallState.Failed;
                componentState.ErrorMessage = run.ErrorMessage;
                result.FailedComponents.Add(component);
                result.ErrorMessage = run.ErrorMessage ?? $"Failed to launch updater for component '{component}'.";
                await _checkpointStore.SaveCheckpointAsync(checkpoint, cancellationToken);
                SetProgress(orchestrationId, "failed", component, result.ErrorMessage, ComputePercent(result.UpdatedComponents.Count, checkpoint.Components.Count), false);
                break;
            }

            // Mark completed
            componentState.State = ComponentInstallState.Completed;
            componentState.UpdatedAtUtc = DateTimeOffset.UtcNow;
            result.UpdatedComponents.Add(component);
            await _checkpointStore.SaveCheckpointAsync(checkpoint, cancellationToken);
            SetProgress(orchestrationId, "installing", component, $"Update applied for {component}.", ComputePercent(result.UpdatedComponents.Count, checkpoint.Components.Count), false);

            _logger.LogInformation(
                "[AUTOUPDATE] Component {Component} completed successfully (index {Index})",
                component,
                i
            );
        }

        result.Success = result.FailedComponents.Count == 0;
        result.CompletedAtUtc = DateTimeOffset.UtcNow;
        return result;
    }

    private static UnifiedInstallCheckpoint CreateCheckpoint(
        string orchestrationId,
        DateTimeOffset startedAt,
        UnifiedInstallUpdateRequest request,
        List<string> plan,
        UnifiedUpdateStatusInfo status)
    {
        var checkpoint = new UnifiedInstallCheckpoint
        {
            OrchestrationId = orchestrationId,
            StartedAtUtc = startedAt,
            LastUpdatedAtUtc = startedAt,
            IsInstalling = false,
            Force = request.Force,
            RequestedBy = request.RequestedBy,
            Components = plan,
            CurrentComponentIndex = 0,
            ComponentStates = new List<ComponentCheckpointState>()
        };

        foreach (var component in plan)
        {
            var componentStatus = status.Components.FirstOrDefault(c =>
                string.Equals(c.Component, component, StringComparison.OrdinalIgnoreCase));

            checkpoint.ComponentStates.Add(new ComponentCheckpointState
            {
                Component = component,
                State = ComponentInstallState.Pending,
                TargetVersion = componentStatus?.LatestVersion ?? "0.0.0.0",
                DownloadUrl = componentStatus?.DownloadUrl ?? string.Empty,
                Sha256 = componentStatus?.Sha256 ?? string.Empty,
                LocalZipPath = null,
                ErrorMessage = null,
                UpdatedAtUtc = null
            });
        }

        return checkpoint;
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