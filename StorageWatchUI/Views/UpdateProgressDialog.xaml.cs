using System.Windows;
using StorageWatchUI.ViewModels;

namespace StorageWatchUI.Views
{
    public partial class UpdateProgressDialog : Window
    {
        public UpdateProgressDialog()
        {
            InitializeComponent();
        }

        public UpdateProgressDialog(UpdateProgressViewModel viewModel) : this()
        {
            DataContext = viewModel;
        }
    }
}
