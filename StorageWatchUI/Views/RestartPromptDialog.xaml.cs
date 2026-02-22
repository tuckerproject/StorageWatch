using System.Windows;

namespace StorageWatchUI.Views;

public partial class RestartPromptDialog : Window
{
    public RestartPromptDialog()
    {
        InitializeComponent();
    }

    private void RestartNowButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }

    private void RestartLaterButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
