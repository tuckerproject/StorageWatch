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
using StorageWatch.Services.DataRetention;
using StorageWatch.Services.Logging;
using StorageWatch.Services.Monitoring;
using StorageWatch.Services.Scheduling;
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
        private readonly IServiceProvider _serviceProvider;
        private readonly RollingFileLogger _logger;
        private readonly SqlReporterScheduler _sqlScheduler;
        private readonly NotificationLoop _notificationLoop;
        private readonly RetentionManager? _retentionManager;

        /// <summary>
        /// Initializes a new instance of the Worker class using dependency injection.
        /// </summary>
        /// <param name="optionsMonitor">Monitor for accessing and observing configuration changes.</param>
        /// <param name="serviceProvider">Service provider for resolving dependencies.</param>
        public Worker(
            IOptionsMonitor<StorageWatchOptions> optionsMonitor,
            IServiceProvider serviceProvider)
        {
            _optionsMonitor = optionsMonitor ?? throw new ArgumentNullException(nameof(optionsMonitor));
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));

            // Create and initialize the logging system to the unified log directory
            var logFilePath = LogDirectoryInitializer.GetLogFilePath("agent.log");
            Directory.CreateDirectory(Path.GetDirectoryName(logFilePath)!);
            _logger = new RollingFileLogger(logFilePath);
            _logger.Log("[DIAG-WORKER] Worker constructor entered.");

            // Get current options snapshot
            var options = _optionsMonitor.CurrentValue;

            // Log startup information if enabled in configuration
            if (options.General.EnableStartupLogging)
                _logger.Log("[STARTUP] Config loaded from JSON");

            // Ensure database directory exists before initializing schema
            var programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
            var storageWatchDir = Path.Combine(programData, "StorageWatch", "Agent");
            Directory.CreateDirectory(storageWatchDir);
            _logger.Log($"[STARTUP] Database directory ensured at {storageWatchDir}");

            // Initialize and verify SQLite database schema
            var schemaInitializer = new SqliteSchema(options.Database.ConnectionString, _logger);
            try
            {
                _logger.Log("[DIAG-WORKER] Starting SQLite database initialization.");
                schemaInitializer.InitializeDatabaseAsync().Wait();
                _logger.Log("[DIAG-WORKER] SQLite database initialization completed successfully.");
                if (options.General.EnableStartupLogging)
                    _logger.Log("[STARTUP] SQLite database initialized");
            }
            catch (Exception ex)
            {
                _logger.Log($"[STARTUP ERROR] Failed to initialize SQLite database: {ex}");
                _logger.Log($"[DIAG-WORKER] SQLite database initialization failed; rethrowing exception. {ex}");
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

            // Initialize the SQL reporter component without forwarder
            var sqlReporter = new SqlReporter(options, _logger);

            // Initialize schedulers for SQL reporting and disk notifications
            _sqlScheduler = new SqlReporterScheduler(options, sqlReporter, _logger);
            _notificationLoop = new NotificationLoop(options, senders, monitor, _logger);

            // Initialize the retention manager for automatic data cleanup
            try
            {
                _logger.Log("[DIAG-WORKER] Starting retention manager initialization.");
                _retentionManager = new RetentionManager(options.Database.ConnectionString, options.Retention, _logger);
                _logger.Log("[DIAG-WORKER] Retention manager initialization completed successfully.");
                if (options.General.EnableStartupLogging)
                    _logger.Log("[STARTUP] Retention manager initialized");
            }
            catch (Exception ex)
            {
                _logger.Log($"[STARTUP ERROR] Failed to initialize retention manager: {ex}");
                _logger.Log($"[DIAG-WORKER] Retention manager initialization failed; rethrowing exception. {ex}");
                throw;
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
            _logger.Log("[DIAG-WORKER] ExecuteAsync entered.");
            bool cancellationObserved = false;

            try
            {
                // Start the notification loop as a background task (disk monitoring and alerts)
                // This runs continuously while respecting the associated stoppingToken
                _logger.Log("[DIAG-WORKER] Starting notification loop task.");
                _ = _notificationLoop.RunAsync(stoppingToken);
                _logger.Log("[DIAG-WORKER] Notification loop task started.");

                // Run the main worker loop: periodically check if SQL reporting should execute
                _logger.Log("[DIAG-WORKER] Checking stoppingToken.IsCancellationRequested for loop condition.");
                while (!stoppingToken.IsCancellationRequested)
                {
                    _logger.Log("[DIAG-WORKER] Loop iteration started.");

                    // Check if the current time matches the scheduled SQL collection time
                    _logger.Log("[DIAG-SCHED] Invoking SqlReporterScheduler.CheckAndRunAsync.");
                    try
                    {
                        await _sqlScheduler.CheckAndRunAsync(DateTime.Now);
                        _logger.Log("[DIAG-SCHED] SqlReporterScheduler.CheckAndRunAsync completed.");
                    }
                    catch (Exception ex)
                    {
                        _logger.Log($"[DIAG-SCHED] SqlReporterScheduler.CheckAndRunAsync failed; rethrowing exception. {ex}");
                        throw;
                    }

                    // Check if retention cleanup should execute (non-blocking)
                    if (_retentionManager != null)
                    {
                        _logger.Log("[DIAG-WORKER] Invoking retention cleanup check.");
                        try
                        {
                            await _retentionManager.CheckAndCleanupAsync();
                            _logger.Log("[DIAG-WORKER] Retention cleanup check completed.");
                        }
                        catch (Exception ex)
                        {
                            _logger.Log($"[WORKER ERROR] Retention cleanup failed: {ex}");
                        }
                    }

                    _logger.Log("[DIAG-WORKER] Loop iteration completed.");

                    // Wait 30 seconds before checking again
                    await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
                    _logger.Log("[DIAG-WORKER] Delay completed.");
                    _logger.Log("[DIAG-WORKER] Checking stoppingToken.IsCancellationRequested for loop condition.");
                }

                if (!cancellationObserved && stoppingToken.IsCancellationRequested)
                {
                    cancellationObserved = true;
                    _logger.Log("[DIAG-WORKER] stoppingToken.IsCancellationRequested transitioned to true.");
                }
            }
            catch (Exception ex)
            {
                _logger.Log($"[DIAG-WORKER] ExecuteAsync encountered fatal exception; rethrowing. {ex}");
                throw;
            }
            finally
            {
                _logger.Log("[DIAG-WORKER] ExecuteAsync exiting.");
            }

            _logger.Log("[WORKER] ExecuteAsync exiting");
        }
    }
}