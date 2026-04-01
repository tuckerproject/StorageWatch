using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using StorageWatchServer.Server.Data;
using StorageWatchServer.Server.Services;
using StorageWatchServer.Services;
using StorageWatchServer.Services.AutoUpdate;
using StorageWatchServer.Services.Logging;

namespace StorageWatchServer.Dashboard;

public class IndexModel : PageModel
{
    private readonly ServerRepository _repository;
    private readonly MachineStatusService _statusService;
    private readonly UnifiedUpdateClient _unifiedUpdateClient;
    private readonly ILogger<IndexModel> _logger;
    private readonly RollingFileLogger? _rollingLogger;

    public IndexModel(
        ServerRepository repository,
        MachineStatusService statusService,
        UnifiedUpdateClient unifiedUpdateClient,
        ILogger<IndexModel> logger,
        RollingFileLogger? rollingLogger = null)
    {
        _repository = repository;
        _statusService = statusService;
        _unifiedUpdateClient = unifiedUpdateClient;
        _logger = logger;
        _rollingLogger = rollingLogger;
    }

    public IReadOnlyList<MachineSummaryView> Machines { get; private set; } = Array.Empty<MachineSummaryView>();

    public string CurrentServerVersion { get; private set; } = "0.0.0.0";
    public string CurrentAgentVersion { get; private set; } = "0.0.0.0";
    public string CurrentUiVersion { get; private set; } = "0.0.0.0";
    public string LatestServerVersion { get; private set; } = "0.0.0.0";
    public string LatestAgentVersion { get; private set; } = "0.0.0.0";
    public string LatestUiVersion { get; private set; } = "0.0.0.0";
    public bool IsUpdateAvailable { get; private set; }

    public string? ErrorMessage { get; private set; }

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        try
        {
            var machines = await _repository.GetMachinesAsync();
            _logger.LogDebug("Loaded {MachineCount} machines for dashboard", machines.Count);

            if (machines.Count == 0)
            {
                _rollingLogger?.Log("[WEBHOST] No machines found in database");
            }

            Machines = machines.Select(machine => new MachineSummaryView
            {
                Id = machine.Id,
                MachineName = machine.MachineName,
                LastSeenUtc = machine.LastSeenUtc,
                IsOnline = _statusService.IsOnline(machine.LastSeenUtc),
                Drives = machine.Drives
            }).ToList();

            await RefreshVersionsAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading machines for dashboard");
            _rollingLogger?.Log($"[WEBHOST] Razor page failed: {ex.Message}");
            ErrorMessage = "An error occurred while loading machines. Please try again later.";
        }
    }

    public async Task<IActionResult> OnGetRefreshVersionsAsync(CancellationToken cancellationToken)
    {
        await RefreshVersionsAsync(cancellationToken);
        return new JsonResult(new
        {
            currentServerVersion = CurrentServerVersion,
            currentAgentVersion = CurrentAgentVersion,
            currentUiVersion = CurrentUiVersion,
            latestServerVersion = LatestServerVersion,
            latestAgentVersion = LatestAgentVersion,
            latestUiVersion = LatestUiVersion,
            isUpdateAvailable = IsUpdateAvailable
        });
    }

    public async Task<IActionResult> OnGetStartUnifiedUpdateAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("[WEB] Starting unified update");

        var progressEvents = new List<ServerUpdateProgressInfo>();
        var progress = new Progress<ServerUpdateProgressInfo>(p => progressEvents.Add(p));

        var result = await _unifiedUpdateClient.StartUnifiedUpdateAsync(progress, cancellationToken);

        if (result.Errors.Count == 0)
        {
            _logger.LogInformation("[WEB] Unified update completed successfully");
        }
        else
        {
            _logger.LogError("[WEB] Unified update failed: {Errors}", string.Join("; ", result.Errors));
        }

        await RefreshVersionsAsync(cancellationToken);

        return new JsonResult(new
        {
            serverUpdated = result.ServerUpdated,
            agentUpdated = result.AgentUpdated,
            uiUpdated = result.UiUpdated,
            errors = result.Errors,
            progress = progressEvents,
            currentServerVersion = CurrentServerVersion,
            currentAgentVersion = CurrentAgentVersion,
            currentUiVersion = CurrentUiVersion,
            latestServerVersion = LatestServerVersion,
            latestAgentVersion = LatestAgentVersion,
            latestUiVersion = LatestUiVersion,
            isUpdateAvailable = IsUpdateAvailable
        });
    }

    private async Task RefreshVersionsAsync(CancellationToken cancellationToken)
    {
        var status = await _unifiedUpdateClient.GetStatusAsync(cancellationToken);
        if (status == null)
            return;

        CurrentServerVersion = string.IsNullOrWhiteSpace(status.CurrentServerVersion)
            ? (string.IsNullOrWhiteSpace(status.CurrentVersion) ? "0.0.0.0" : status.CurrentVersion)
            : status.CurrentServerVersion;
        CurrentAgentVersion = string.IsNullOrWhiteSpace(status.CurrentAgentVersion) ? "0.0.0.0" : status.CurrentAgentVersion;
        CurrentUiVersion = string.IsNullOrWhiteSpace(status.CurrentUiVersion) ? "0.0.0.0" : status.CurrentUiVersion;

        LatestServerVersion = string.IsNullOrWhiteSpace(status.LatestServerVersion)
            ? (string.IsNullOrWhiteSpace(status.LatestVersion) ? "0.0.0.0" : status.LatestVersion)
            : status.LatestServerVersion;
        LatestAgentVersion = string.IsNullOrWhiteSpace(status.LatestAgentVersion) ? "0.0.0.0" : status.LatestAgentVersion;
        LatestUiVersion = string.IsNullOrWhiteSpace(status.LatestUiVersion) ? "0.0.0.0" : status.LatestUiVersion;

        IsUpdateAvailable = status.UpdateAvailable;
    }
}

public class MachineSummaryView
{
    public int Id { get; init; }

    public required string MachineName { get; init; }

    public DateTime LastSeenUtc { get; init; }

    public bool IsOnline { get; init; }

    public IReadOnlyList<StorageWatchServer.Server.Models.MachineDriveStatus> Drives { get; init; } = Array.Empty<StorageWatchServer.Server.Models.MachineDriveStatus>();
}
