using StorageWatch.Config.Options;
using StorageWatch.Data;
using StorageWatch.Services.Logging;
using System.Threading;
using System.Threading.Tasks;

namespace StorageWatch.Services.CentralServer
{
    /// <summary>
    /// Central Server Service
    /// 
    /// DEPRECATED: This service is no longer used in the Agent project.
    /// Server functionality has been moved to the StorageWatchServer project.
    /// This class is kept for backward compatibility but is not instantiated.
    /// </summary>
    public class CentralServerService
    {
        private readonly CentralServerOptions _options;
        private readonly RollingFileLogger _logger;
        private readonly CentralServerRepository _repository;

        /// <summary>
        /// Initializes a new instance of the CentralServerService class.
        /// </summary>
        public CentralServerService(
            CentralServerOptions options,
            RollingFileLogger logger,
            CentralServerRepository repository)
        {
            _options = options;
            _logger = logger;
            _repository = repository;
        }

        /// <summary>
        /// Starts the central server HTTP listener.
        /// DEPRECATED: This method is no longer functional in the Agent project.
        /// </summary>
        public async Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.Log("[CentralServer] Server mode is not supported in Agent. Use StorageWatchServer project.");
            await Task.CompletedTask;
        }

        /// <summary>
        /// Stops the central server HTTP listener.
        /// DEPRECATED: This method is no longer functional in the Agent project.
        /// </summary>
        public async Task StopAsync(CancellationToken cancellationToken)
        {
            await Task.CompletedTask;
        }
    }
}
