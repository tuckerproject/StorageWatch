using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using DiskSpaceService.Config;
using DiskSpaceService.Services;
using DiskSpaceService.Data;
using DiskSpaceService.Services.Logging;
using DiskSpaceService.Services.Alerts;
using DiskSpaceService.Scheduling;

namespace DiskSpaceService
{
    public class Worker : BackgroundService
    {
        private readonly ILogger<Worker> _eventLogger;

        public Worker(ILogger<Worker> eventLogger)
        {
            _eventLogger = eventLogger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _eventLogger.LogInformation("Disk Space Service starting...");

            var collector = new DiskSpaceCollector();
            var missedRunDetector = new MissedRunDetector();

            string logDir = @"C:\ProgramData\DiskSpaceService\Logs";
            var fileLogger = new RollingFileLogger(logDir);

            DailyRunScheduler? scheduler = null;
            DiskSpaceAlertMonitor? alertMonitor = null;

            var alertStateStore = new AlertStateStore(
                Path.Combine(logDir, "AlertState.json"));

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var config = ConfigManager.Load();
                    var writer = new DatabaseWriter(config.Database.ConnectionString);
                    var groupMe = new GroupMeAlertService(config.Alert.GroupMeBotId);

                    scheduler ??= new DailyRunScheduler(
                        collector,
                        writer,
                        missedRunDetector,
                        fileLogger,
                        _eventLogger,
                        groupMe);

                    alertMonitor ??= new DiskSpaceAlertMonitor(
                        collector,
                        groupMe,
                        fileLogger,
                        alertStateStore);

                    // Daily run
                    await scheduler.ExecuteCycleAsync(config, stoppingToken);

                    // Continuous alert monitoring
                    await alertMonitor.CheckAsync(config);
                }
                catch (Exception ex)
                {
                    _eventLogger.LogError(ex, "Unexpected error in service loop.");
                    fileLogger.Log($"ERROR: {ex.Message}");
                }

                try
                {
                    await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
                }
                catch (TaskCanceledException)
                {
                    break;
                }
            }

            _eventLogger.LogInformation("Disk Space Service stopping...");
        }
    }
}