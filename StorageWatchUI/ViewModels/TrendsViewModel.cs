using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;
using StorageWatchUI.Models;
using StorageWatchUI.Services;
using System.Collections.ObjectModel;
using System.Windows.Input;

namespace StorageWatchUI.ViewModels;

/// <summary>
/// ViewModel for the Trends view showing historical disk usage charts.
/// </summary>
public class TrendsViewModel : ViewModelBase
{
    private readonly IDataProvider _dataProvider;
    private bool _isLoading;
    private string _statusMessage = string.Empty;
    private string? _selectedDrive;
    private int _selectedDaysBack = 7;

    public TrendsViewModel(IDataProvider dataProvider)
    {
        _dataProvider = dataProvider;

        RefreshCommand = new RelayCommand(async () => await LoadDataAsync());
        LoadDrivesCommand = new RelayCommand(async () => await LoadAvailableDrivesAsync());
    }

    public ObservableCollection<string> AvailableDrives { get; } = new();

    public ISeries[] Series { get; private set; } = Array.Empty<ISeries>();

    public string? SelectedDrive
    {
        get => _selectedDrive;
        set
        {
            if (SetProperty(ref _selectedDrive, value))
            {
                _ = LoadDataAsync();
            }
        }
    }

    public int SelectedDaysBack
    {
        get => _selectedDaysBack;
        set
        {
            if (SetProperty(ref _selectedDaysBack, value))
            {
                _ = LoadDataAsync();
            }
        }
    }

    public bool IsLoading
    {
        get => _isLoading;
        set => SetProperty(ref _isLoading, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public ICommand RefreshCommand { get; }
    public ICommand LoadDrivesCommand { get; }

    private async Task LoadAvailableDrivesAsync()
    {
        try
        {
            var drives = await _dataProvider.GetMonitoredDrivesAsync();
            AvailableDrives.Clear();
            foreach (var drive in drives)
            {
                AvailableDrives.Add(drive);
            }

            if (AvailableDrives.Any() && string.IsNullOrEmpty(SelectedDrive))
            {
                SelectedDrive = AvailableDrives.First();
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error loading drives: {ex.Message}";
        }
    }

    private async Task LoadDataAsync()
    {
        if (string.IsNullOrEmpty(SelectedDrive))
        {
            await LoadAvailableDrivesAsync();
            return;
        }

        IsLoading = true;
        StatusMessage = "Loading trend data...";

        try
        {
            var trends = await _dataProvider.GetTrendDataAsync(SelectedDrive, SelectedDaysBack);

            if (!trends.Any())
            {
                StatusMessage = "No trend data available for the selected period.";
                Series = Array.Empty<ISeries>();
                OnPropertyChanged(nameof(Series));
                return;
            }

            // Create chart series
            Series = new ISeries[]
            {
                new LineSeries<TrendDataPoint>
                {
                    Values = trends,
                    Mapping = (dataPoint, index) => new(index, dataPoint.PercentFree),
                    Name = "% Free Space",
                    Fill = null,
                    Stroke = new SolidColorPaint(SKColors.DodgerBlue) { StrokeThickness = 2 },
                    GeometrySize = 4
                }
            };

            OnPropertyChanged(nameof(Series));
            StatusMessage = $"Showing {trends.Count} data points for {SelectedDrive}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error loading trend data: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }
}
