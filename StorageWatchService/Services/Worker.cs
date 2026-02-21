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
using StorageWatch.Services.DataRetention;
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
        private readonly IOptionsMonitor<CentralServerOptions> _centralServerOptions;
        private readonly IServiceProvider _serviceProvider;
        private readonly RollingFileLogger _logger;
        private readonly SqlReporterScheduler _sqlScheduler;
        private readonly NotificationLoop _notificationLoop;
        private readonly CentralServerService? _centralServer;
        private readonly RetentionManager? _retentionManager;

        /// <summary>
        /// Initializes a new instance of the Worker class using dependency injection.
        /// </summary>
        /// <param name="optionsMonitor">Monitor for accessing and observing configuration changes.</param>
        /// <param name="serviceProvider">Service provider for resolving dependencies.</param>
        public Worker(
            IOptionsMonitor<StorageWatchOptions> optionsMonitor,
            IOptionsMonitor<CentralServerOptions> centralServerOptions,
            IServiceProvider serviceProvider)
        {
            _optionsMonitor = optionsMonitor ?? throw new ArgumentNullException(nameof(optionsMonitor));
            _centralServerOptions = centralServerOptions ?? throw new ArgumentNullException(nameof(centralServerOptions));
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));

            var programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
            var storageWatchDir = Path.Combine(programData, "StorageWatch");
            var logDir = Path.Combine(storageWatchDir, "Logs");

            // Create and initialize the logging system at the common application data directory
            Directory.CreateDirectory(logDir);
            _logger = new RollingFileLogger(Path.Combine(logDir, "service.log"));

            // Get current options snapshot
            var options = _optionsMonitor.CurrentValue;
            var centralOptions = _centralServerOptions.CurrentValue;

            // Log startup information if enabled in configuration
            if (options.General.EnableStartupLogging)
                _logger.Log("[STARTUP] Config loaded from JSON");

            // Ensure database directory exists before initializing schema
            Directory.CreateDirectory(storageWatchDir);
            _logger.Log($"[STARTUP] Database directory ensured at {storageWatchDir}");

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

            // Build the list of alert senders using the new plugin architecture
            var pluginManager = AlertSenderFactory.CreatePluginManager(_serviceProvider, options, _logger);
            var senders = pluginManager.GetEnabledSenders();

            if (options.General.EnableStartupLogging)
            {
                _logger.Log($"[STARTUP] Loaded {senders.Count} alert sender plugin(s)");
                foreach (var sender in senders)
                {
                    _logger.Log($"[STARTUP]   - {sender.Name}");
                }
            }

            // Initialize the central server forwarder if in agent mode
            CentralServerForwarder? forwarder = null;
            if (centralOptions.Enabled &&
                centralOptions.Mode.Equals("Agent", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    forwarder = new CentralServerForwarder(centralOptions, _logger);
                    if (options.General.EnableStartupLogging)
                        _logger.Log($"[STARTUP] Central server forwarder initialized for {centralOptions.ServerUrl}");
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

            // Initialize the retention manager for automatic data cleanup
            try
            {
                _retentionManager = new RetentionManager(options.Database.ConnectionString, options.Retention, _logger);
                if (options.General.EnableStartupLogging)
                    _logger.Log("[STARTUP] Retention manager initialized");
            }
            catch (Exception ex)
            {
                _logger.Log($"[STARTUP ERROR] Failed to initialize retention manager: {ex}");
                throw;
            }

            // Initialize central server if enabled in server mode
            if (centralOptions.Enabled &&
                centralOptions.Mode.Equals("Server", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    var centralSchema = new CentralServerSchema(centralOptions.CentralConnectionString, _logger);
                    centralSchema.InitializeDatabaseAsync().Wait();
                    if (options.General.EnableStartupLogging)
                        _logger.Log("[STARTUP] Central server database initialized");

                    var centralRepository = new CentralServerRepository(centralOptions.CentralConnectionString, _logger);
                    _centralServer = new CentralServerService(centralOptions, _logger, centralRepository);

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

                // Check if retention cleanup should execute (non-blocking)
                if (_retentionManager != null)
                {
                    try
                    {
                        await _retentionManager.CheckAndCleanupAsync();
                    }
                    catch (Exception ex)
                    {
                        _logger.Log($"[WORKER ERROR] Retention cleanup failed: {ex}");
                    }
                }

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