using Microsoft.AspNetCore.Mvc.RazorPages;
using StorageWatchServer.Server.Data;
using StorageWatchServer.Server.Models;
using Microsoft.Extensions.Logging;

namespace StorageWatchServer.Dashboard;

public class SettingsModel : PageModel
{
    private readonly ServerRepository _repository;
    private readonly ILogger<SettingsModel> _logger;

    public SettingsModel(ServerRepository repository, ILogger<SettingsModel> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public IReadOnlyList<SettingRecord> Settings { get; private set; } = Array.Empty<SettingRecord>();

    public string? ErrorMessage { get; private set; }

    public async Task OnGetAsync()
    {
        try
        {
            Settings = await _repository.GetSettingsAsync();
            _logger.LogDebug("Loaded {SettingCount} settings", Settings.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading settings");
            ErrorMessage = "An error occurred while loading settings.";
        }
    }
}
