using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using DiskSpaceService.Config;
using DiskSpaceService.Data;
using DiskSpaceService.Services;
using DiskSpaceService.Services.Logging;
using DiskSpaceService.Services.Alerts;
using Microsoft.Extensions.Logging;

namespace DiskSpaceService.Scheduling
{
    public class DailyRunScheduler
    {
        private readonly DiskSpaceCollector _collector;
        private readonly DatabaseWriter _writer;
        private readonly MissedRunDetector _missedRunDetector;
        private readonly RollingFileLogger _fileLogger;
        private readonly ILogger _eventLogger;
        private readonly GroupMeAlertService _groupMe;

        private string _lastDecision = "";

        public DailyRunScheduler(
            DiskSpaceCollector collector,
            DatabaseWriter writer,
            MissedRunDetector missedRunDetector,
            RollingFileLogger fileLogger,
            ILogger eventLogger,
            GroupMeAlertService groupMe)
        {
            _collector = collector;
            _writer = writer;
            _missedRunDetector = missedRunDetector;
            _fileLogger = fileLogger;
            _eventLogger = eventLogger;
            _groupMe = groupMe;
        }

        public async Task ExecuteCycleAsync(ServiceConfig config, CancellationToken stoppingToken)
        {
            DateTime nowLocal = DateTime.Now;

            if (!DateTime.TryParseExact(
                    config.CollectionTime,
                    "HH:mm",
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.None,
                    out DateTime scheduledLocalTime))
            {
                LogDecision("ERROR: Invalid CollectionTime format.");
                return;
            }

            DateTime todayScheduledLocal = DateTime.Today
                .AddHours(scheduledLocalTime.Hour)
                .AddMinutes(scheduledLocalTime.Minute);

            DateTime lastRunUtc = _missedRunDetector.LoadLastRunUtc();
            DateTime? lastRunLocal = lastRunUtc == DateTime.MinValue
                ? null
                : lastRunUtc.ToLocalTime();

            bool hasRunToday = lastRunLocal?.Date == nowLocal.Date;

            string decision;

            if (hasRunToday)
            {
                decision = "Skip: already ran today";
                LogDecision(decision);
                return;
            }

            if (nowLocal < todayScheduledLocal)
            {
                decision = "Skip: too early";
                LogDecision(decision);
                return;
            }

            if (!config.RunMissedCollection && nowLocal > todayScheduledLocal)
            {
                decision = "Skip: missed run but RunMissedCollection=false";
                LogDecision(decision);
                return;
            }

            decision = "Run: scheduled or missed run";
            LogDecision(decision);

            try
            {
                var metrics = _collector.Collect(config.Drives);
                await _writer.InsertAsync(metrics);
                _missedRunDetector.SaveLastRunUtc();

                _eventLogger.LogInformation("Disk space metrics successfully written to database.");
                _fileLogger.Log("Run completed successfully.");

                // Alert logic
                foreach (var m in metrics)
                {
                    if (m.PercentFree < config.Alert.ThresholdPercent)
                    {
                        string alert =
                            $"⚠️ LOW DISK SPACE ALERT\n" +
                            $"Machine: {m.MachineName}\n" +
                            $"Drive: {m.DriveLetter}\n" +
                            $"Free: {m.PercentFree}%\n" +
                            $"Threshold: {config.Alert.ThresholdPercent}%";

                        await _groupMe.SendMessageAsync(alert);
                        _fileLogger.Log($"GroupMe alert sent for drive {m.DriveLetter} ({m.PercentFree}% free).");
                    }
                }
            }
            catch (Exception ex)
            {
                _eventLogger.LogError(ex, "Error during disk space collection.");
                _fileLogger.Log($"ERROR: {ex.Message}");
            }
        }

        private void LogDecision(string decision)
        {
            if (decision != _lastDecision)
            {
                _fileLogger.Log(decision);
                _lastDecision = decision;
            }
        }
    }
}