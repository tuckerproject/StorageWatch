using Microsoft.AspNetCore.Mvc.RazorPages;

namespace StorageWatchServer.Dashboard;

public class ReportsModel : PageModel
{
    public int DefaultCount { get; } = 50;
}
