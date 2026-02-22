using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Diagnostics;

namespace StorageWatchServer.Services.AutoUpdate
{
    public interface IServerRestartHandler
    {
        void RequestRestart();
    }

    public class ServerRestartHandler : IServerRestartHandler
    {
        private readonly ILogger<ServerRestartHandler> _logger;
        private readonly IHostApplicationLifetime _lifetime;

        public ServerRestartHandler(ILogger<ServerRestartHandler> logger, IHostApplicationLifetime lifetime)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _lifetime = lifetime ?? throw new ArgumentNullException(nameof(lifetime));
        }

        public void RequestRestart()
        {
            var processPath = Environment.ProcessPath;
            if (string.IsNullOrWhiteSpace(processPath))
            {
                _logger.LogWarning("Unable to restart server: process path unavailable.");
                return;
            }

            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = processPath,
                    UseShellExecute = true
                });

                _lifetime.StopApplication();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Server restart failed");
            }
        }
    }
}
