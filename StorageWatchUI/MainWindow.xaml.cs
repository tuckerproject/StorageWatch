using StorageWatchUI.ViewModels;
using System.Windows;

namespace StorageWatchUI;

public partial class MainWindow : Window
{
    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
