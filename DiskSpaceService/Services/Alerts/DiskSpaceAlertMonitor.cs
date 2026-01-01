using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DiskSpaceService.Config;
using DiskSpaceService.Services.Logging;

namespace DiskSpaceService.Services.Alerts
{
    public class DiskSpaceAlertMonitor
    {
        private readonly DiskSpaceCollector _collector;
        private readonly GroupMeAlertService _groupMe;
        private readonly RollingFileLogger _logger;
        private readonly AlertStateStore _stateStore;

        private Dictionary<string, string> _state;

        public DiskSpaceAlertMonitor(
            DiskSpaceCollector collector,
            GroupMeAlertService groupMe,
            RollingFileLogger logger,
            AlertStateStore stateStore)
        {
            _collector = collector;
            _groupMe = groupMe;
            _logger = logger;
            _stateStore = stateStore;

            _state = _stateStore.Load();
        }

        public async Task CheckAsync(ServiceConfig config)
        {
            var metrics = _collector.Collect(config.Drives);

            foreach (var m in metrics)
            {
                string drive = m.DriveLetter;
                decimal threshold = config.Alert.ThresholdPercent;

                string previousState = _state.ContainsKey(drive) ? _state[drive] : "NORMAL";
                string newState = m.PercentFree < threshold ? "ALERT" : "NORMAL";

                if (previousState == newState)
                    continue;

                if (newState == "ALERT")
                {
                    string msg =
                        $"⚠️ LOW DISK SPACE\n" +
                        $"Machine: {m.MachineName}\n" +
                        $"Drive: {drive}\n" +
                        $"Free: {m.PercentFree}%\n" +
                        $"Threshold: {threshold}%";

                    await _groupMe.SendMessageAsync(msg);
                    _logger.Log($"ALERT SENT: Drive {drive} below threshold ({m.PercentFree}%).");
                }
                else
                {
                    string msg =
                        $"✔️ DISK SPACE NORMAL\n" +
                        $"Machine: {m.MachineName}\n" +
                        $"Drive: {drive}\n" +
                        $"Free: {m.PercentFree}%\n" +
                        $"Threshold: {threshold}%";

                    await _groupMe.SendMessageAsync(msg);
                    _logger.Log($"NORMAL SENT: Drive {drive} recovered ({m.PercentFree}%).");
                }

                _state[drive] = newState;
                _stateStore.Save(_state);
            }
        }
    }
}