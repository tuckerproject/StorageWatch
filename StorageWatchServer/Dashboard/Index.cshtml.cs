using Microsoft.AspNetCore.Mvc.RazorPages;
using StorageWatchServer.Server.Data;
using StorageWatchServer.Server.Services;
using StorageWatchServer.Services.Logging;

namespace StorageWatchServer.Dashboard;

public class IndexModel : PageModel
{
    private readonly ServerRepository _repository;
    private readonly MachineStatusService _statusService;
    private readonly ILogger<IndexModel> _logger;
    private readonly RollingFileLogger? _rollingLogger;

    public IndexModel(ServerRepository repository, MachineStatusService statusService, ILogger<IndexModel> logger, RollingFileLogger? rollingLogger = null)
    {
        _repository = repository;
        _statusService = statusService;
        _logger = logger;
        _rollingLogger = rollingLogger;
    }

    public IReadOnlyList<MachineSummaryView> Machines { get; private set; } = Array.Empty<MachineSummaryView>();

    public string? ErrorMessage { get; private set; }

    public async Task OnGetAsync()
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
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading machines for dashboard");
            _rollingLogger?.Log($"[WEBHOST] Razor page failed: {ex.Message}");
            ErrorMessage = "An error occurred while loading machines. Please try again later.";
        }
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
