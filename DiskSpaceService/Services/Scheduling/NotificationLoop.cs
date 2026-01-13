using DiskSpaceService.Config;
using DiskSpaceService.Models;
using DiskSpaceService.Services.Alerting;
using DiskSpaceService.Services.Logging;
using DiskSpaceService.Services.Monitoring;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace DiskSpaceService.Services.Scheduling
{
    public class NotificationLoop
    {
        private readonly DiskSpaceConfig _config;
        private readonly List<IAlertSender> _senders;
        private readonly DiskAlertMonitor _monitor;
        private readonly RollingFileLogger _logger;

        private readonly Dictionary<string, string> _lastAlertState = new();

        public NotificationLoop(
            DiskSpaceConfig config,
            List<IAlertSender> senders,
            DiskAlertMonitor monitor,
            RollingFileLogger logger)
        {
            _config = config;
            _senders = senders;
            _monitor = monitor;
            _logger = logger;
        }

        public async Task RunAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    foreach (var driveLetter in _config.Drives)
                    {
                        var status = _monitor.GetStatus(driveLetter);

                        bool belowThreshold = status.PercentFree < _config.ThresholdPercent;
                        string state = belowThreshold ? "ALERT" : "NORMAL";

                        _lastAlertState.TryGetValue(driveLetter, out var lastState);

                        if (lastState != state)
                        {
                            if (belowThreshold)
                            {
                                _logger.Log(
                                    $"[ALERT] ALERT SENT: Drive {status.DriveName} below threshold ({status.PercentFree:F2}%)."
                                );

                                string message =
                                    $"ALERT: Drive {status.DriveName} is below threshold. " +
                                    $"{status.FreeSpaceGb:F2} GB free ({status.PercentFree:F2}%).";

                                foreach (var sender in _senders)
                                    await sender.SendAlertAsync(message);
                            }
                            else
                            {
                                _logger.Log(
                                    $"[ALERT] NORMAL SENT: Drive {status.DriveName} recovered ({status.PercentFree:F2}%)."
                                );

                                string message =
                                    $"RECOVERY: Drive {status.DriveName} has recovered. " +
                                    $"{status.FreeSpaceGb:F2} GB free ({status.PercentFree:F2}%).";

                                foreach (var sender in _senders)
                                    await sender.SendAlertAsync(message);
                            }

                            _lastAlertState[driveLetter] = state;
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.Log($"[ALERT] Notification loop error: {ex}");
                }

                await Task.Delay(TimeSpan.FromMinutes(1), token);
            }
        }
    }
}