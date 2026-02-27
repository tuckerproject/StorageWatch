using StorageWatch.Services.Logging;
using System;
using System.Diagnostics;

namespace StorageWatch.Services.AutoUpdate
{
    public interface IServiceRestartHandler
    {
        void RequestRestart();
    }

    public class ServiceRestartHandler : IServiceRestartHandler
    {
        private readonly RollingFileLogger _logger;

        public ServiceRestartHandler(RollingFileLogger logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public void RequestRestart()
        {
            var processPath = Environment.ProcessPath;
            if (string.IsNullOrWhiteSpace(processPath))
            {
                _logger.Log("[AUTOUPDATE] Unable to restart service: process path unavailable.");
                return;
            }

            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = processPath,
                    UseShellExecute = true
                });

                Environment.Exit(0);
            }
            catch (Exception ex)
            {
                _logger.Log($"[AUTOUPDATE] Service restart failed: {ex}");
            }
        }
    }
}
