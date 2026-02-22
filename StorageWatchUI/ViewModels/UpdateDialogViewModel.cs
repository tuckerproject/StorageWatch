using System.Windows.Input;

namespace StorageWatchUI.ViewModels;

/// <summary>
/// ViewModel for the update dialog, displaying update details.
/// </summary>
public class UpdateDialogViewModel : ViewModelBase
{
    private string _currentVersion = string.Empty;
    private string _newVersion = string.Empty;
    private string _releaseNotes = string.Empty;

    public UpdateDialogViewModel()
    {
        UpdateCommand = new RelayCommand(OnUpdate);
        CancelCommand = new RelayCommand(OnCancel);
    }

    public string CurrentVersion
    {
        get => _currentVersion;
        set => SetProperty(ref _currentVersion, value);
    }

    public string NewVersion
    {
        get => _newVersion;
        set => SetProperty(ref _newVersion, value);
    }

    public string ReleaseNotes
    {
        get => _releaseNotes;
        set => SetProperty(ref _releaseNotes, value);
    }

    public ICommand UpdateCommand { get; }
    public ICommand CancelCommand { get; }

    public event EventHandler? UpdateRequested;
    public event EventHandler? CancelRequested;

    private void OnUpdate()
    {
        UpdateRequested?.Invoke(this, EventArgs.Empty);
    }

    private void OnCancel()
    {
        CancelRequested?.Invoke(this, EventArgs.Empty);
    }
}
