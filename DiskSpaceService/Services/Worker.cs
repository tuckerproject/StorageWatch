/// <summary>
/// Main Background Worker Service
/// 
/// This is the primary orchestrator service for the DiskSpaceService application.
/// It initializes all components (configuration, logging, monitoring, alerting, and scheduling),
/// and manages two concurrent tasks: the notification loop (disk monitoring and alerting)
/// and the SQL reporting scheduler.
/// </summary>

using DiskSpaceService.Config;
using DiskSpaceService.Services.Alerting;
using DiskSpaceService.Services.Logging;
using DiskSpaceService.Services.Monitoring;
using DiskSpaceService.Services.Scheduling;
using Microsoft.Extensions.Hosting;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace DiskSpaceService.Services
{
    /// <summary>
    /// The main background service that runs continuously as a Windows Service.
    /// Coordinates disk monitoring, alerting, and SQL reporting operations.
    /// </summary>
    public class Worker : BackgroundService
    {
        private readonly DiskSpaceConfig _config;
        private readonly RollingFileLogger _logger;
        private readonly SqlReporterScheduler _sqlScheduler;
        private readonly NotificationLoop _notificationLoop;

        /// <summary>
        /// Initializes a new instance of the Worker class.
        /// Performs initialization of logger, configuration loading, and component setup.
        /// </summary>
        public Worker()
        {
            // Create and initialize the logging system at the common application data directory
            string logDir = @"C:\ProgramData\DiskSpaceService\Logs";
            Directory.CreateDirectory(logDir);
            _logger = new RollingFileLogger(Path.Combine(logDir, "service.log"));

            // Load configuration from the XML config file located next to the executable
            var baseDir = AppContext.BaseDirectory;
            var configPath = Path.Combine(baseDir, "DiskSpaceConfig.xml");
            _config = ConfigLoader.Load(configPath);

            // Log startup information if enabled in configuration
            if (_config.EnableStartupLogging)
                _logger.Log("[STARTUP] Config loaded");

            // Initialize the disk monitoring component
            var monitor = new DiskAlertMonitor(_config);
            
            // Build the list of alert senders (e.g., GroupMe, SMTP) based on configuration
            var senders = AlertSenderFactory.BuildSenders(_config, _logger);
            
            // Initialize the SQL reporter component
            var sqlReporter = new SqlReporter(_config, _logger);

            // Initialize schedulers for SQL reporting and disk notifications
            _sqlScheduler = new SqlReporterScheduler(_config, sqlReporter, _logger);
            _notificationLoop = new NotificationLoop(_config, senders, monitor, _logger);

            // Log when components have been initialized
            if (_config.EnableStartupLogging)
                _logger.Log("[STARTUP] Components initialized");
        }

        /// <summary>
        /// Executes the main work loop of the service.
        /// Runs two concurrent tasks: continuous notification monitoring and periodic SQL reporting checks.
        /// </summary>
        /// <param name="stoppingToken">Token for signaling when the service should stop.</param>
        /// <returns>A task representing the work loop execution.</returns>
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.Log("[WORKER] ExecuteAsync started");

            // Start the notification loop as a background task (disk monitoring and alerts)
            // This runs continuously while respecting the associated stoppingToken
            _ = _notificationLoop.RunAsync(stoppingToken);

            // Run the main worker loop: periodically check if SQL reporting should execute
            while (!stoppingToken.IsCancellationRequested)
            {
                // Check if the current time matches the scheduled SQL collection time
                await _sqlScheduler.CheckAndRunAsync(DateTime.Now);
                
                // Wait 30 seconds before checking again
                await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
            }

            _logger.Log("[WORKER] ExecuteAsync exiting");
        }
    }
}