using Microsoft.AspNetCore.Mvc.RazorPages;
using StorageWatchServer.Server.Data;
using StorageWatchServer.Server.Models;

namespace StorageWatchServer.Dashboard;

public class AlertsModel : PageModel
{
    private readonly ServerRepository _repository;

    public AlertsModel(ServerRepository repository)
    {
        _repository = repository;
    }

    public IReadOnlyList<AlertRecord> Alerts { get; private set; } = Array.Empty<AlertRecord>();

    public async Task OnGetAsync()
    {
        Alerts = await _repository.GetAlertsAsync();
    }
}
