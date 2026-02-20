using System.Text.Json;
using Microsoft.AspNetCore.Mvc.RazorPages;
using StorageWatchServer.Server.Data;
using StorageWatchServer.Server.Models;
using StorageWatchServer.Server.Services;

namespace StorageWatchServer.Dashboard.Machines;

public class DetailsModel : PageModel
{
    private readonly ServerRepository _repository;
    private readonly MachineStatusService _statusService;

    public DetailsModel(ServerRepository repository, MachineStatusService statusService)
    {
        _repository = repository;
        _statusService = statusService;
    }

    public MachineDetails? Machine { get; private set; }

    public bool IsOnline { get; private set; }

    public IReadOnlyList<DriveChartView> DriveCharts { get; private set; } = Array.Empty<DriveChartView>();

    public string ChartDataJson { get; private set; } = "[]";

    public async Task OnGetAsync(int id)
    {
        Machine = await _repository.GetMachineAsync(id);
        if (Machine == null)
        {
            return;
        }

        IsOnline = _statusService.IsOnline(Machine.LastSeenUtc);
        var chartPayload = new List<object>();

        var charts = new List<DriveChartView>();
        foreach (var drive in Machine.Drives)
        {
            var history = await _repository.GetDiskHistoryAsync(id, drive.DriveLetter, DateTime.UtcNow.AddDays(-7));
            var chartId = drive.DriveLetter.Replace(":", string.Empty).Replace("\\", string.Empty);
            charts.Add(new DriveChartView
            {
                DriveLetter = drive.DriveLetter,
                ChartId = chartId,
                History = history
            });

            chartPayload.Add(new
            {
                drive = chartId,
                labels = history.Select(point => point.CollectionTimeUtc.ToString("MM-dd HH:mm")).ToList(),
                values = history.Select(point => point.PercentFree).ToList()
            });
        }

        DriveCharts = charts;
        ChartDataJson = JsonSerializer.Serialize(chartPayload);
    }
}

public class DriveChartView
{
    public required string DriveLetter { get; init; }

    public required string ChartId { get; init; }

    public IReadOnlyList<DiskHistoryPoint> History { get; init; } = Array.Empty<DiskHistoryPoint>();
}
