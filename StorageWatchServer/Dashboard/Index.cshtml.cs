using Microsoft.AspNetCore.Mvc.RazorPages;
using StorageWatchServer.Server.Data;
using StorageWatchServer.Server.Services;

namespace StorageWatchServer.Dashboard;

public class IndexModel : PageModel
{
    private readonly ServerRepository _repository;
    private readonly MachineStatusService _statusService;

    public IndexModel(ServerRepository repository, MachineStatusService statusService)
    {
        _repository = repository;
        _statusService = statusService;
    }

    public IReadOnlyList<MachineSummaryView> Machines { get; private set; } = Array.Empty<MachineSummaryView>();

    public async Task OnGetAsync()
    {
        var machines = await _repository.GetMachinesAsync();
        Machines = machines.Select(machine => new MachineSummaryView
        {
            Id = machine.Id,
            MachineName = machine.MachineName,
            LastSeenUtc = machine.LastSeenUtc,
            IsOnline = _statusService.IsOnline(machine.LastSeenUtc),
            Drives = machine.Drives
        }).ToList();
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
