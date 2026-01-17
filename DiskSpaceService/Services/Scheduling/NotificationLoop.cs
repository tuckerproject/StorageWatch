using DiskSpaceService.Config;
using DiskSpaceService.Models;
using DiskSpaceService.Services.Alerting;
using DiskSpaceService.Services.Logging;
using DiskSpaceService.Services.Monitoring;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
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
        private readonly string _stateFilePath =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "DiskSpaceService", "alert_state.json");

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

            Directory.CreateDirectory(Path.GetDirectoryName(_stateFilePath)!);
            LoadState();
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

                        string state;
                        string message;

                        if (status.TotalSpaceGb == 0)
                        {
                            state = "NOT_READY";
                            message = $"ALERT: Drive {status.DriveName} is NOT READY or unavailable.";
                        }
                        else
                        {
                            bool belowThreshold = status.PercentFree < _config.ThresholdPercent;

                            if (belowThreshold)
                            {
                                state = "ALERT";
                                message =
                                    $"ALERT: Drive {status.DriveName} is below threshold. " +
                                    $"{status.FreeSpaceGb:F2} GB free ({status.PercentFree:F2}%).";
                            }
                            else
                            {
                                state = "NORMAL";
                                message =
                                    $"RECOVERY: Drive {status.DriveName} has recovered. " +
                                    $"{status.FreeSpaceGb:F2} GB free ({status.PercentFree:F2}%).";
                            }
                        }

                        _lastAlertState.TryGetValue(driveLetter, out var lastState);

                        if (lastState != state)
                        {
                            // Only send if network is ready
                            if (NetworkReady())
                            {
                                foreach (var sender in _senders)
                                    await sender.SendAlertAsync(message);

                                _logger.Log($"[ALERT] State change for {driveLetter}: {lastState} → {state}");
                                _lastAlertState[driveLetter] = state;
                                SaveState();
                            }
                            else
                            {
                                _logger.Log("[ALERT] Network not ready — delaying alert send.");
                            }
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

        private bool NetworkReady()
        {
            try
            {
                Dns.GetHostEntry("api.groupme.com");
                return true;
            }
            catch
            {
                return false;
            }
        }

        private void LoadState()
        {
            try
            {
                if (File.Exists(_stateFilePath))
                {
                    var json = File.ReadAllText(_stateFilePath);
                    var dict = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(json);
                    if (dict != null)
                        foreach (var kv in dict)
                            _lastAlertState[kv.Key] = kv.Value;
                }
            }
            catch { }
        }

        private void SaveState()
        {
            try
            {
                var json = System.Text.Json.JsonSerializer.Serialize(_lastAlertState);
                File.WriteAllText(_stateFilePath, json);
            }
            catch { }
        }
    }
}