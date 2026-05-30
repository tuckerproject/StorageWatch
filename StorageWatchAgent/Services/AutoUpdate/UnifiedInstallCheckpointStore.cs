using System.Text.Json;
using Microsoft.Extensions.Logging;
using StorageWatchAgent.Services.AutoUpdate.Models;

namespace StorageWatchAgent.Services.AutoUpdate;

/// <summary>
/// Interface for persisting and loading unified install checkpoints.
/// </summary>
public interface IUnifiedInstallCheckpointStore
{
    /// <summary>
    /// Save the current checkpoint state to disk.
    /// </summary>
    Task SaveCheckpointAsync(UnifiedInstallCheckpoint checkpoint, CancellationToken cancellationToken = default);

    /// <summary>
    /// Load the current checkpoint state from disk, or null if none exists.
    /// </summary>
    Task<UnifiedInstallCheckpoint?> LoadCheckpointAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Clear the checkpoint (delete the persisted file).
    /// </summary>
    Task ClearCheckpointAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Check whether a checkpoint exists.
    /// </summary>
    Task<bool> CheckpointExistsAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Disk-backed checkpoint store using JSON serialization.
/// </summary>
public class UnifiedInstallCheckpointStore : IUnifiedInstallCheckpointStore
{
    private const string CheckpointFileName = "install-plan.json";
    private static readonly string CheckpointDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
        "StorageWatch",
        "Update"
    );

    private readonly ILogger<UnifiedInstallCheckpointStore> _logger;
    private readonly SemaphoreSlim _lock = new(1, 1);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public UnifiedInstallCheckpointStore(ILogger<UnifiedInstallCheckpointStore> logger)
    {
        _logger = logger;
    }

    private string CheckpointFilePath => Path.Combine(CheckpointDirectory, CheckpointFileName);

    public async Task SaveCheckpointAsync(UnifiedInstallCheckpoint checkpoint, CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            checkpoint.LastUpdatedAtUtc = DateTimeOffset.UtcNow;

            Directory.CreateDirectory(CheckpointDirectory);
            var json = JsonSerializer.Serialize(checkpoint, JsonOptions);

            // Atomic write: write to temp file then replace
            var tempPath = CheckpointFilePath + ".tmp";
            await File.WriteAllTextAsync(tempPath, json, cancellationToken);
            File.Move(tempPath, CheckpointFilePath, overwrite: true);

            _logger.LogInformation(
                "Saved checkpoint for orchestration {OrchestrationId}, index {Index}/{Total}, installing={Installing}",
                checkpoint.OrchestrationId,
                checkpoint.CurrentComponentIndex,
                checkpoint.Components.Count,
                checkpoint.IsInstalling
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save checkpoint for orchestration {OrchestrationId}", checkpoint.OrchestrationId);
            throw;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<UnifiedInstallCheckpoint?> LoadCheckpointAsync(CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            if (!File.Exists(CheckpointFilePath))
            {
                _logger.LogDebug("No checkpoint file found at {Path}", CheckpointFilePath);
                return null;
            }

            var json = await File.ReadAllTextAsync(CheckpointFilePath, cancellationToken);
            var checkpoint = JsonSerializer.Deserialize<UnifiedInstallCheckpoint>(json, JsonOptions);

            if (checkpoint != null)
            {
                _logger.LogInformation(
                    "Loaded checkpoint for orchestration {OrchestrationId}, index {Index}/{Total}, installing={Installing}",
                    checkpoint.OrchestrationId,
                    checkpoint.CurrentComponentIndex,
                    checkpoint.Components.Count,
                    checkpoint.IsInstalling
                );
            }

            return checkpoint;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load checkpoint from {Path}", CheckpointFilePath);
            return null;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task ClearCheckpointAsync(CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            if (File.Exists(CheckpointFilePath))
            {
                File.Delete(CheckpointFilePath);
                _logger.LogInformation("Cleared checkpoint at {Path}", CheckpointFilePath);
            }
            else
            {
                _logger.LogDebug("No checkpoint to clear at {Path}", CheckpointFilePath);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to clear checkpoint at {Path}", CheckpointFilePath);
            throw;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<bool> CheckpointExistsAsync(CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            return File.Exists(CheckpointFilePath);
        }
        finally
        {
            _lock.Release();
        }
    }
}
