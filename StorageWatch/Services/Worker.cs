/// <summary>
/// Main Background Worker Service
/// 
/// This is the primary orchestrator service for the StorageWatch application.
/// It initializes all components (configuration, logging, monitoring, alerting, and scheduling),
/// and manages two concurrent tasks: the notification loop (disk monitoring and alerting)
/// and the SQL reporting scheduler.
/// </summary>

using StorageWatch.Config;
using StorageWatch.Config.Options;
using StorageWatch.Data;
using StorageWatch.Services.Alerting;
using StorageWatch.Services.Logging;
using StorageWatch.Services.Monitoring;
using StorageWatch.Services.Scheduling;
using StorageWatch.Services.CentralServer;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace StorageWatch.Services
{
    /// <summary>
    /// The main background service that runs continuously as a Windows Service.
    /// Coordinates disk monitoring, alerting, and SQL reporting operations.
    /// </summary>
    public class Worker : BackgroundService
    {
        private readonly IOptionsMonitor<StorageWatchOptions> _optionsMonitor;
        private readonly RollingFileLogger _logger;
        private readonly SqlReporterScheduler _sqlScheduler;
        private readonly NotificationLoop _notificationLoop;
        private readonly CentralServerService? _centralServer;

        /// <summary>
        /// Initializes a new instance of the Worker class using dependency injection.
        /// </summary>
        /// <param name="optionsMonitor">Monitor for accessing and observing configuration changes.</param>
        public Worker(IOptionsMonitor<StorageWatchOptions> optionsMonitor)
        {
            _optionsMonitor = optionsMonitor ?? throw new ArgumentNullException(nameof(optionsMonitor));

            // Create and initialize the logging system at the common application data directory
            string logDir = @"C:\ProgramData\StorageWatch\Logs";
            Directory.CreateDirectory(logDir);
            _logger = new RollingFileLogger(Path.Combine(logDir, "service.log"));

            // Get current options snapshot
            var options = _optionsMonitor.CurrentValue;

            // Log startup information if enabled in configuration
            if (options.General.EnableStartupLogging)
                _logger.Log("[STARTUP] Config loaded from JSON");

            // Initialize and verify SQLite database schema
            var schemaInitializer = new SqliteSchema(options.Database.ConnectionString, _logger);
            try
            {
                schemaInitializer.InitializeDatabaseAsync().Wait();
                if (options.General.EnableStartupLogging)
                    _logger.Log("[STARTUP] SQLite database initialized");
            }
            catch (Exception ex)
            {
                _logger.Log($"[STARTUP ERROR] Failed to initialize SQLite database: {ex}");
                throw;
            }

            // Initialize the disk monitoring component
            var monitor = new DiskAlertMonitor(options);
            
            // Build the list of alert senders (e.g., GroupMe, SMTP) based on configuration
            var senders = AlertSenderFactory.BuildSenders(options, _logger);
            
            // Initialize the central server forwarder if in agent mode
            CentralServerForwarder? forwarder = null;
            if (options.CentralServer.Enabled && 
                options.CentralServer.Mode.Equals("Agent", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    forwarder = new CentralServerForwarder(options.CentralServer, _logger);
                    if (options.General.EnableStartupLogging)
                        _logger.Log($"[STARTUP] Central server forwarder initialized for {options.CentralServer.ServerUrl}");
                }
                catch (Exception ex)
                {
                    _logger.Log($"[STARTUP ERROR] Failed to initialize central server forwarder: {ex}");
                    throw;
                }
            }
            
            // Initialize the SQL reporter component with optional forwarder
            var sqlReporter = new SqlReporter(options, _logger, forwarder);

            // Initialize schedulers for SQL reporting and disk notifications
            _sqlScheduler = new SqlReporterScheduler(options, sqlReporter, _logger);
            _notificationLoop = new NotificationLoop(options, senders, monitor, _logger);

            // Initialize central server if enabled in server mode
            if (options.CentralServer.Enabled && 
                options.CentralServer.Mode.Equals("Server", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    var centralSchema = new CentralServerSchema(options.CentralServer.CentralConnectionString, _logger);
                    centralSchema.InitializeDatabaseAsync().Wait();
                    if (options.General.EnableStartupLogging)
                        _logger.Log("[STARTUP] Central server database initialized");

                    var centralRepository = new CentralServerRepository(options.CentralServer.CentralConnectionString, _logger);
                    _centralServer = new CentralServerService(options.CentralServer, _logger, centralRepository);

                    if (options.General.EnableStartupLogging)
                        _logger.Log("[STARTUP] Central server initialized");
                }
                catch (Exception ex)
                {
                    _logger.Log($"[STARTUP ERROR] Failed to initialize central server: {ex}");
                    throw;
                }
            }
            else
            {
                _centralServer = null;
            }

            // Log when components have been initialized
            if (options.General.EnableStartupLogging)
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

            // Start the central server if enabled
            if (_centralServer != null)
            {
                try
                {
                    await _centralServer.StartAsync(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.Log($"[WORKER ERROR] Central server failed to start: {ex}");
                }
            }

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

            // Stop the central server if it was running
            if (_centralServer != null)
            {
                try
                {
                    await _centralServer.StopAsync();
                }
                catch (Exception ex)
                {
                    _logger.Log($"[WORKER ERROR] Failed to stop central server: {ex}");
                }
            }

            _logger.Log("[WORKER] ExecuteAsync exiting");
        }
    }
}