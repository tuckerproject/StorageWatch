using StorageWatchUI.ViewModels;
using StorageWatchUI.Services.Logging;
using System.Windows;

namespace StorageWatchUI;

public partial class MainWindow : Window
{
    private readonly RollingFileLogger? _logger;

    public MainWindow(MainViewModel viewModel, RollingFileLogger? logger = null)
    {
        _logger = logger;
        try
        {
            InitializeComponent();
            _logger?.Log("[UI] MainWindow initialized");
            DataContext = viewModel;
        }
        catch (Exception ex)
        {
            _logger?.Log($"[ERROR] UI error during MainWindow initialization: {ex.Message}");
            throw;
        }
    }
}
