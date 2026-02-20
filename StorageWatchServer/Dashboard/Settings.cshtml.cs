using Microsoft.AspNetCore.Mvc.RazorPages;
using StorageWatchServer.Server.Data;
using StorageWatchServer.Server.Models;

namespace StorageWatchServer.Dashboard;

public class SettingsModel : PageModel
{
    private readonly ServerRepository _repository;

    public SettingsModel(ServerRepository repository)
    {
        _repository = repository;
    }

    public IReadOnlyList<SettingRecord> Settings { get; private set; } = Array.Empty<SettingRecord>();

    public async Task OnGetAsync()
    {
        Settings = await _repository.GetSettingsAsync();
    }
}
