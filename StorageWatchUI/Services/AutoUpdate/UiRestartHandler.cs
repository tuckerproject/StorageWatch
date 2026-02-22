using Microsoft.Extensions.Logging;
using System;
using System.Diagnostics;
using System.Windows;

namespace StorageWatchUI.Services.AutoUpdate
{
    public interface IUiRestartPrompter
    {
        bool PromptForRestart();
    }

    public interface IUiRestartHandler
    {
        void RequestRestart();
    }

    public class UiRestartPrompter : IUiRestartPrompter
    {
        public bool PromptForRestart()
        {
            var result = MessageBox.Show(
                "An update has been installed. Restart StorageWatch UI now?",
                "Update Installed",
                MessageBoxButton.YesNo,
                MessageBoxImage.Information);

            return result == MessageBoxResult.Yes;
        }
    }

    public class UiRestartHandler : IUiRestartHandler
    {
        private readonly ILogger<UiRestartHandler> _logger;

        public UiRestartHandler(ILogger<UiRestartHandler> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public void RequestRestart()
        {
            var processPath = Environment.ProcessPath;
            if (string.IsNullOrWhiteSpace(processPath))
            {
                _logger.LogWarning("Unable to restart UI: process path unavailable.");
                return;
            }

            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = processPath,
                    UseShellExecute = true
                });

                Application.Current.Shutdown();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "UI restart failed");
            }
        }
    }
}
