using System;
using System.Collections.Generic;
using System.IO;
using DiskSpaceService.Models;

namespace DiskSpaceService.Services
{
    public class DiskSpaceCollector
    {
        public List<DiskMetrics> Collect(IEnumerable<string> driveLetters)
        {
            var results = new List<DiskMetrics>();
            string machineName = Environment.MachineName;

            foreach (var letter in driveLetters)
            {
                string driveRoot = $"{letter}:\\";

                try
                {
                    var drive = new DriveInfo(letter);

                    if (!drive.IsReady)
                        continue;

                    decimal totalGB = BytesToGB(drive.TotalSize);
                    decimal freeGB = BytesToGB(drive.TotalFreeSpace);
                    decimal usedGB = totalGB - freeGB;

                    var metrics = new DiskMetrics
                    {
                        MachineName = machineName,
                        DriveLetter = letter,
                        TotalSpaceGB = totalGB,
                        UsedSpaceGB = usedGB,
                        FreeSpaceGB = freeGB,
                        PercentFree = totalGB > 0 ? Math.Round((freeGB / totalGB) * 100, 2) : 0
                    };

                    results.Add(metrics);
                }
                catch (Exception ex)
                {
                    // You can add logging here later
                    Console.WriteLine($"Error reading drive {driveRoot}: {ex.Message}");
                }
            }

            return results;
        }

        private decimal BytesToGB(long bytes)
        {
            return Math.Round(bytes / 1024m / 1024m / 1024m, 2);
        }
    }
}