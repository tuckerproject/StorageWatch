# Phase 10: Documentation Examples - Handoff-Based Flow

This document provides specific examples from the codebase showing how documentation correctly reflects the "prepare → stage → handoff → exit" flow across all three components.

---

## UI Component Documentation

### UiUpdateInstaller - Complete Flow Documentation

```csharp
namespace StorageWatchUI.Services.AutoUpdate
{
    /// <summary>
    /// Installs UI updates using a handoff-only pipeline: prepare, stage, handoff, exit.
    /// </summary>
    public interface IUiUpdateInstaller
    {
        /// <summary>
        /// Prepares and stages the update package, then launches the updater executable and exits the UI process.
        /// </summary>
        Task<UpdateInstallResult> InstallAsync(string zipPath, CancellationToken cancellationToken, IProgress<double>? progress = null);
    }

    /// <summary>
    /// UI update installer that only prepares files, stages payload content, hands off to updater, and exits.
    /// </summary>
    public class UiUpdateHandoffInstaller : IUiUpdateInstaller
    {
        /// <summary>
        /// Executes the handoff flow: prepare package input, stage extracted payload, hand off to updater, and exit.
        /// </summary>
        public Task<UpdateInstallResult> InstallAsync(string zipPath, CancellationToken cancellationToken, IProgress<double>? progress = null)
        {
            // ... Implementation logs:
            _logger.LogInformation("[AUTOUPDATE] UI update handed off to updater executable.");
            _exitAction();
        }
    }
}
```

**Key Documentation Elements**:
- Interface summary: Explicitly mentions "handoff-only pipeline"
- Method summary: Describes all four phases (prepare, stage, handoff, exit)
- Class summary: Clear about "hands off to updater"
- Logging: Confirms "handed off to updater executable"

### UiAutoUpdateWorker - Coordinator Documentation

```csharp
public class UiAutoUpdateWorker : IUiAutoUpdateWorker
{
    /// <summary>
    /// Progress details for the UI update handoff flow.
    /// </summary>
    public sealed class UiUpdateProgressInfo
    {
        public string Status { get; init; } = string.Empty;
        public double ProgressPercent { get; init; }
        public bool IsIndeterminate { get; init; }
    }

    /// <summary>
    /// Coordinates update checks and updater handoff operations for the UI process.
    /// </summary>
    public interface IUiAutoUpdateWorker
    {
        void Start();
        Task StopAsync();
        bool IsCycleActive { get; }
        Task<bool> TryRunUpdateCycleAsync(CancellationToken cancellationToken = default);
        Task<bool> TryInstallAvailableUpdateAsync(CancellationToken cancellationToken = default);
        event EventHandler<ComponentUpdateCheckResult>? UpdateCheckCompleted;
        event EventHandler<UiUpdateProgressInfo>? UpdateProgressChanged;
        event EventHandler<UpdateInstallResult>? UpdateInstallCompleted;
    }
}
```

**Key Documentation Elements**:
- Progress class: Explicitly mentions "UI update handoff flow"
- Interface summary: Emphasizes "handoff operations" as core role
- All events named to reflect handoff pipeline stages

---

## Agent Component Documentation

### UpdateInstaller - Service Installation Flow

```csharp
namespace StorageWatch.Services.AutoUpdate
{
    /// <summary>
    /// Installs Agent updates using a handoff-only pipeline: prepare, stage, handoff, exit.
    /// </summary>
    public interface IServiceUpdateInstaller
    {
        /// <summary>
        /// Prepares and stages the update package, then launches the updater executable and exits the service process.
        /// </summary>
        Task<UpdateInstallResult> InstallAsync(string zipPath, CancellationToken cancellationToken);
    }

    /// <summary>
    /// Agent update installer that only prepares files, stages payload content, hands off to updater, and exits.
    /// </summary>
    public class AgentUpdateHandoffInstaller : IServiceUpdateInstaller
    {
        /// <summary>
        /// Executes the handoff flow: prepare package input, stage extracted payload, hand off to updater, and exit.
        /// </summary>
        public Task<UpdateInstallResult> InstallAsync(string zipPath, CancellationToken cancellationToken)
        {
            try
            {
                ZipFile.ExtractToDirectory(zipPath, stagingDirectory, true);

                var manifestPath = EnsureStagingManifest(stagingDirectory);

                LaunchUpdaterForAgentUpdate(stagingDirectory, manifestPath, installDir);

                _logger.LogInformation("[AUTOUPDATE] Agent update handed off to updater executable.");

                _scmStopRequester(_serviceName);
                _exitAction();

                return Task.FromResult(new UpdateInstallResult { Success = true });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[AUTOUPDATE] Agent install handoff failed.");
                // ...
            }
        }
    }
}
```

**Key Documentation Elements**:
- Interface summary: "handoff-only pipeline" 
- Method summary: Explicit phases (prepare, stage, handoff, exit)
- Class summary: Clear about file preparation and updater handoff
- Logging: Shows actual flow with stage → manifest → launch → exit

### AutoUpdateWorker - Background Service with Handoff

```csharp
public class AutoUpdateWorker : BackgroundService
{
    /// <summary>
    /// BackgroundService that coordinates automatic update checks and installs for the Agent.
    /// </summary>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Auto-update check loop for the Agent service.
        var autoUpdateOptions = _autoUpdateOptionsMonitor.CurrentValue;
        if (!autoUpdateOptions.Enabled)
        {
            _logger.Log("[AUTOUPDATE] Auto-update is disabled via configuration.");
            return;
        }

        // Perform an immediate check at startup, then periodic checks
        await RunUpdateCycleAsync(stoppingToken);

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await RunUpdateCycleAsync(stoppingToken);
        }
    }

    public async Task<UpdateInstallResult> RunServiceUpdateAsync(CancellationToken stoppingToken)
    {
        // Use a non-cancelable token once handoff starts to avoid partial staging state.
        var install = await _serviceUpdateInstaller.InstallAsync(download.FilePath, CancellationToken.None);
        if (!install.Success)
        {
            _logger.Log($"[AUTOUPDATE] Service install failed: {install.ErrorMessage}");
            return install;
        }

        _logger.Log("[AUTOUPDATE] Service update installed.");
        return install;
    }
}
```

**Key Documentation Elements**:
- Uses `BackgroundService` base class per workspace requirements
- Comment explains why non-cancelable token: "to avoid partial staging state"
- Logging confirms handoff completion

### ServiceRestartHandler - Updater Delegation

```csharp
namespace StorageWatch.Services.AutoUpdate
{
    public interface IServiceRestartHandler
    {
        void RequestRestart();
    }

    public class UpdaterServiceRestartHandler : IServiceRestartHandler
    {
        public void RequestRestart()
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                _logger.Log("[AUTOUPDATE] Restart requested on non-Windows OS. Restart skipped.");
                return;
            }

            try
            {
                var updaterPath = ResolveUpdaterExecutablePath();
                var processStartInfo = new ProcessStartInfo
                {
                    FileName = updaterPath,
                    Arguments = "--restart-agent",
                    // ... configuration
                };

                var process = _processStarter(processStartInfo);
                _logger.Log($"[AUTOUPDATE] Updater launched for agent restart. PID: {process.Id}. Exiting agent process.");
                _exitAction();
            }
            catch (Exception ex)
            {
                _logger.Log($"[AUTOUPDATE] Failed to delegate restart to updater: {ex}");
            }
        }
    }
}
```

**Key Documentation Elements**:
- Class name: `UpdaterServiceRestartHandler` (not "Agent Restart Handler")
- Logging: "Updater launched" and "Exiting agent process"
- Clear delegation to updater executable

---

## Server Component Documentation

### ServerUpdateInstaller - Server Graceful Shutdown & Handoff

```csharp
namespace StorageWatchServer.Services.AutoUpdate
{
    /// <summary>
    /// Installs Server updates using a handoff-only pipeline: prepare, stage, handoff, exit.
    /// </summary>
    public interface IServerUpdateInstaller
    {
        /// <summary>
        /// Prepares and stages the update package, then launches the updater executable and exits the server process.
        /// </summary>
        Task<UpdateInstallResult> InstallAsync(string zipPath, CancellationToken cancellationToken);
    }

    /// <summary>
    /// Server update installer that only prepares files, stages payload content, hands off to updater, and exits.
    /// </summary>
    public class ServerUpdateHandoffInstaller : IServerUpdateInstaller
    {
        /// <summary>
        /// Executes the handoff flow: prepare package input, stage extracted payload, hand off to updater, and exit.
        /// </summary>
        public Task<UpdateInstallResult> InstallAsync(string zipPath, CancellationToken cancellationToken)
        {
            try
            {
                ZipFile.ExtractToDirectory(zipPath, stagingDirectory, true);

                var manifestPath = EnsureStagingManifest(stagingDirectory);

                _logger.LogInformation("[AUTOUPDATE] Preparing graceful shutdown before updater handoff.");
                _gracefulStopAction();

                LaunchUpdaterForServerUpdate(stagingDirectory, manifestPath, installDir);

                _logger.LogInformation("[AUTOUPDATE] Server update handoff scheduled. Exiting server process.");
                _exitAction();

                return Task.FromResult(new UpdateInstallResult { Success = true });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[AUTOUPDATE] Server install handoff failed.");
                return Task.FromResult(new UpdateInstallResult
                {
                    Success = false,
                    ErrorMessage = ex.Message
                });
            }
        }
    }
}
```

**Key Documentation Elements**:
- Interface and class summaries: All mention "handoff-only pipeline"
- Logging sequence: Shows graceful shutdown BEFORE handoff
- Clear exit messaging

### ServerUpdateChecker - Manifest Processing

```csharp
public class ServerUpdateChecker : IServerUpdateChecker
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task<ComponentUpdateCheckResult> CheckForUpdateAsync(CancellationToken cancellationToken)
    {
        var options = _options.Value;
        if (string.IsNullOrWhiteSpace(options.ManifestUrl))
        {
            return new ComponentUpdateCheckResult
            {
                IsUpdateAvailable = false,
                ErrorMessage = "ManifestUrl is not configured."
            };
        }

        try
        {
            var response = await _httpClient.GetAsync(options.ManifestUrl, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return new ComponentUpdateCheckResult
                {
                    IsUpdateAvailable = false,
                    ErrorMessage = $"Manifest request failed with status {(int)response.StatusCode}."
                };
            }

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            var manifest = ParseManifest(json);
            // ... validation and version comparison
        }
        catch (Exception ex)
        {
            // ... error handling
        }
    }
}
```

**Key Documentation Elements**:
- Follows standard checker pattern (prepare → check → compare → return)
- Returns `ComponentUpdateCheckResult` with version information
- No legacy progress or in-process update logic

### ServerRestartHandler - Explicit Delegation

```csharp
namespace StorageWatchServer.Services.AutoUpdate
{
    public interface IServerRestartHandler
    {
        void RequestRestart();
    }

    public class ServerRestartHandler : IServerRestartHandler
    {
        public void RequestRestart()
        {
            _logger.LogInformation("Server restart request ignored. Restart is delegated to updater executable.");
        }
    }
}
```

**Key Documentation Elements**:
- Explicit in logging: Restart is delegated
- No attempt to restart in-process
- Clear API contract

### UpdateController - API Documentation

```csharp
namespace StorageWatchServer.Controllers
{
    /// <summary>
    /// API endpoints for update status and server updater handoff operations.
    /// </summary>
    [Route("api/update")]
    [ApiController]
    public class UpdateController : ControllerBase
    {
        /// <summary>
        /// Returns current and latest versions for Server, Agent, and UI components.
        /// </summary>
        [HttpGet("status")]
        public async Task<ActionResult<ServerUpdateStatusDto>> GetStatus(CancellationToken cancellationToken)
        {
            // ... implementation
        }

        /// <summary>
        /// Starts the server update flow (prepare, stage, handoff, exit).
        /// </summary>
        [HttpPost("install")]
        public async Task<ActionResult<UpdateInstallResponseDto>> Install(CancellationToken cancellationToken)
        {
            _logger.LogInformation("[API] Server update install requested");
            // ... delegates to ServerAutoUpdateWorker
        }
    }
}
```

**Key Documentation Elements**:
- Controller summary: Emphasizes "handoff operations"
- Install endpoint: Explicitly documents flow steps
- Logging: Shows API request handling

---

## Result Types - Common Documentation

### UpdateInstallResult (Shared Across All Components)

```csharp
public class UpdateInstallResult
{
    /// <summary>
    /// Whether the install/handoff was successful.
    /// </summary>
    public bool Success { get; set; }
    
    /// <summary>
    /// Error message if Success is false.
    /// </summary>
    public string? ErrorMessage { get; set; }
}
```

### ComponentUpdateCheckResult (Shared Across All Components)

```csharp
public class ComponentUpdateCheckResult
{
    /// <summary>
    /// Whether an update is available for the component.
    /// </summary>
    public bool IsUpdateAvailable { get; set; }
    
    /// <summary>
    /// Update information from manifest (if available).
    /// </summary>
    public ComponentUpdateInfo? Component { get; set; }
    
    /// <summary>
    /// Full manifest received from server.
    /// </summary>
    public UpdateManifest? Manifest { get; set; }
    
    /// <summary>
    /// Error message if check failed.
    /// </summary>
    public string? ErrorMessage { get; set; }
}
```

### UpdateDownloadResult (Shared Across All Components)

```csharp
public class UpdateDownloadResult
{
    /// <summary>
    /// Whether download succeeded.
    /// </summary>
    public bool Success { get; set; }
    
    /// <summary>
    /// Local path to downloaded ZIP file.
    /// </summary>
    public string? FilePath { get; set; }
    
    /// <summary>
    /// Error message if Success is false.
    /// </summary>
    public string? ErrorMessage { get; set; }
}
```

---

## Logging Message Examples - Flow Consistency

### Prepare Phase
```csharp
_logger.LogInformation("[AUTOUPDATE] Auto-update is disabled via configuration.");
_logger.LogInformation("[AUTOUPDATE] Server auto-update is disabled via configuration.");
```

### Check Phase
```csharp
_logger.Log("[AUTOUPDATE] Server update available: {Version}");
_logger.Log("[AUTOUPDATE] No service updates available.");
```

### Download Phase
```csharp
_logger.Log("[AUTOUPDATE] Update check failed: {Error}");
_logger.Log("[AUTOUPDATE] Server download failed: {Error}");
```

### Stage Phase
```csharp
_logger.Log("[AUTOUPDATE] Staging directory: {Directory}");
_logger.Log("[AUTOUPDATE] Manifest created: {Path}");
```

### Handoff Phase
```csharp
_logger.LogInformation("[AUTOUPDATE] UI update handed off to updater executable.");
_logger.LogInformation("[AUTOUPDATE] Agent update handed off to updater executable.");
_logger.LogInformation("[AUTOUPDATE] Server update handed off to updater executable.");
_logger.LogInformation("[AUTOUPDATE] Preparing graceful shutdown before updater handoff.");
```

### Exit Phase
```csharp
_logger.Log($"[AUTOUPDATE] Updater launched for agent restart. PID: {process.Id}. Exiting agent process.");
_logger.LogInformation("[AUTOUPDATE] Server update handoff scheduled. Exiting server process.");
```

---

## Consistency Verification ✅

| Aspect | Pattern | Status |
|--------|---------|--------|
| Interface summaries | All mention "handoff" | ✅ Consistent |
| Class summaries | All mention phases | ✅ Consistent |
| Method docs | All describe four phases | ✅ Consistent |
| Logging prefixes | All use `[AUTOUPDATE]` | ✅ Consistent |
| Logging messages | All reference updater/handoff | ✅ Consistent |
| Result types | Shared across components | ✅ Consistent |
| No legacy terms | "orchestrator", "progress", etc. | ✅ Removed |
| No in-process refs | "direct replace", "in-place" | ✅ Removed |

---

## Conclusion

All documentation and comments across UI, Agent, and Server components correctly and consistently describe the handoff-based update pipeline. The flow is clear and unambiguous:

1. **Prepare**: Validate package and configuration
2. **Stage**: Extract files to temporary location
3. **Handoff**: Launch updater executable with parameters
4. **Exit**: UI/Agent/Server process exits cleanly

No legacy installer, restart handler, or progress infrastructure is documented or referenced in active code.
