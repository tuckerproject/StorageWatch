using System.Windows;
using StorageWatchUI.ViewModels;

namespace StorageWatchUI.Views
{
    public partial class UpdateDialog : Window
    {
        public UpdateDialog()
        {
            InitializeComponent();
        }

        public UpdateDialog(UpdateDialogViewModel viewModel) : this()
        {
            DataContext = viewModel;
        }
    }
}
