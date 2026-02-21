using Microsoft.AspNetCore.Mvc.RazorPages;
using StorageWatchServer.Server.Data;
using StorageWatchServer.Server.Models;

namespace StorageWatchServer.Dashboard;

public class AlertsModel : PageModel
{
    private readonly ServerRepository _repository;
    private readonly ILogger<AlertsModel> _logger;

    public AlertsModel(ServerRepository repository, ILogger<AlertsModel> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public IReadOnlyList<AlertRecord> Alerts { get; private set; } = Array.Empty<AlertRecord>();

    public string? ErrorMessage { get; private set; }

    public async Task OnGetAsync()
    {
        try
        {
            Alerts = await _repository.GetAlertsAsync();
            _logger.LogDebug("Loaded {AlertCount} alerts", Alerts.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading alerts");
            ErrorMessage = "An error occurred while loading alerts.";
        }
    }
}
