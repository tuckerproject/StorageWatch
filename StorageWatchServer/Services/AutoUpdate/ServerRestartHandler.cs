using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;

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
            _logger.LogInformation("Server restart request ignored. Restart is delegated to updater executable.");
        }
    }
}
