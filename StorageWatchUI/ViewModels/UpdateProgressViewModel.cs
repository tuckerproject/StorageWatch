using System.Windows.Input;

namespace StorageWatchUI.ViewModels;

/// <summary>
/// ViewModel for displaying update progress.
/// </summary>
public class UpdateProgressViewModel : ViewModelBase
{
    private string _statusText = string.Empty;
    private double _progress;
    private bool _isIndeterminate = true;

    public UpdateProgressViewModel()
    {
        CancelCommand = new RelayCommand(OnCancel);
    }

    public string StatusText
    {
        get => _statusText;
        set => SetProperty(ref _statusText, value);
    }

    public double Progress
    {
        get => _progress;
        set => SetProperty(ref _progress, value);
    }

    public bool IsIndeterminate
    {
        get => _isIndeterminate;
        set => SetProperty(ref _isIndeterminate, value);
    }

    public ICommand CancelCommand { get; }

    public event EventHandler? CancelRequested;

    private void OnCancel()
    {
        CancelRequested?.Invoke(this, EventArgs.Empty);
    }
}
