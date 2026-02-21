/// <summary>
/// Disk Alert Monitor
/// 
/// Provides pure disk status information without any alerting logic or state tracking.
/// This class is responsible for reading physical disk metrics (total space, free space)
/// and converting them to a standardized DiskStatus model. All alerting decisions and
/// logging are handled by the NotificationLoop, keeping this class focused on its
/// single responsibility: gathering accurate disk metrics.
/// </summary>

using StorageWatch.Config.Options;
using StorageWatch.Models;
using System;
using System.IO;

namespace StorageWatch.Services.Monitoring
{
    /// <summary>
    /// Pure disk status provider. 
    /// No logging, no threshold logic, no state tracking.
    /// NotificationLoop handles all alerting and logging.
    /// </summary>
    public class DiskAlertMonitor
    {
        private readonly MonitoringOptions _monitoringOptions;

        public DiskAlertMonitor(StorageWatchOptions options)
        {
            _monitoringOptions = options?.Monitoring ?? throw new ArgumentNullException(nameof(options));
        }

        public DiskStatus GetStatus(string driveLetter)
        {
            try
            {
                var di = new DriveInfo(driveLetter);

                if (!di.IsReady)
                {
                    return new DiskStatus
                    {
                        DriveName = driveLetter,
                        TotalSpaceGb = 0,
                        FreeSpaceGb = 0
                    };
                }

                double totalGb = di.TotalSize / 1024d / 1024d / 1024d;
                double freeGb = di.AvailableFreeSpace / 1024d / 1024d / 1024d;

                return new DiskStatus
                {
                    DriveName = driveLetter,
                    TotalSpaceGb = totalGb,
                    FreeSpaceGb = freeGb
                };
            }
            catch
            {
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