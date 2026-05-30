using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using StorageWatch.Services.AutoUpdate;

namespace StorageWatchAgent.Services.AutoUpdate;

/// <summary>
/// Startup service that detects pending checkpoints and triggers resume if safe.
/// Resume only occurs if:
/// 1. Checkpoint exists and IsInstalling = true
/// 2. No other install is currently in progress
/// </summary>
public class UnifiedInstallResumeService : IHostedService
{
    private readonly IUnifiedInstallCheckpointStore _checkpointStore;
    private readonly IUnifiedInstallOrchestrator _orchestrator;
    private readonly ILogger<UnifiedInstallResumeService> _logger;

    public UnifiedInstallResumeService(
        IUnifiedInstallCheckpointStore checkpointStore,
        IUnifiedInstallOrchestrator orchestrator,
        ILogger<UnifiedInstallResumeService> logger)
    {
        _checkpointStore = checkpointStore;
        _orchestrator = orchestrator;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            var checkpoint = await _checkpointStore.LoadCheckpointAsync(cancellationToken);

            if (checkpoint == null)
            {
                _logger.LogDebug("No checkpoint found on startup; no resume needed");
                return;
            }

            if (!checkpoint.IsInstalling)
            {
                _logger.LogInformation(
                    "Checkpoint found for orchestration {OrchestrationId} but IsInstalling=false; clearing stale checkpoint",
                    checkpoint.OrchestrationId
                );
                await _checkpointStore.ClearCheckpointAsync(cancellationToken);
                return;
            }

            _logger.LogInformation(
                "Checkpoint found for orchestration {OrchestrationId} with IsInstalling=true; triggering resume from index {Index}/{Total}",
                checkpoint.OrchestrationId,
                checkpoint.CurrentComponentIndex,
                checkpoint.Components.Count
            );

            // Trigger async resume without blocking startup
            _ = Task.Run(async () =>
            {
                try
                {
                    await _orchestrator.ResumePendingInstallAsync(CancellationToken.None);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Resume failed for orchestration {OrchestrationId}", checkpoint.OrchestrationId);
                }
            }, CancellationToken.None);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to check for pending checkpoint on startup");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        // No cleanup needed
        return Task.CompletedTask;
    }
}
