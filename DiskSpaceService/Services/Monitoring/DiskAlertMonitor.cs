using DiskSpaceService.Config;
using DiskSpaceService.Models;
using DiskSpaceService.Services.Logging;
using System;
using System.Collections.Generic;
using System.IO;

namespace DiskSpaceService.Services.Monitoring
{
    public class DiskAlertMonitor
    {
        private readonly DiskSpaceConfig _config;
        private readonly RollingFileLogger _logger;

        private readonly Dictionary<string, double> _lastFreeGb = new();

        public DiskAlertMonitor(DiskSpaceConfig config, RollingFileLogger logger)
        {
            _config = config;
            _logger = logger;
        }

        public DiskStatus GetStatus(string driveLetter)
        {
            try
            {
                var di = new DriveInfo(driveLetter);

                if (!di.IsReady)
                {
                    _logger.Log($"[MONITOR] Drive {driveLetter} is not ready.");
                    return new DiskStatus
                    {
                        DriveName = driveLetter,
                        TotalSpaceGb = 0,
                        FreeSpaceGb = 0
                    };
                }

                double totalGb = di.TotalSize / 1024d / 1024d / 1024d;
                double freeGb = di.AvailableFreeSpace / 1024d / 1024d / 1024d;

                if (!_lastFreeGb.TryGetValue(driveLetter, out var last) ||
                    Math.Abs(last - freeGb) >= 0.1)
                {
                    _logger.Log(
                        $"[MONITOR] Drive {driveLetter}: Free {freeGb:F2} GB of {totalGb:F2} GB ({(freeGb / totalGb) * 100:F2}%)."
                    );

                    _lastFreeGb[driveLetter] = freeGb;
                }

                return new DiskStatus
                {
                    DriveName = driveLetter,
                    TotalSpaceGb = totalGb,
                    FreeSpaceGb = freeGb
                };
            }
            catch (Exception ex)
            {
                _logger.Log($"[MONITOR] Error reading drive {driveLetter}: {ex}");

                return new DiskStatus
                {
                    DriveName = driveLetter,
                    TotalSpaceGb = 0,
                    FreeSpaceGb = 0
                };
            }
        }
    }
}