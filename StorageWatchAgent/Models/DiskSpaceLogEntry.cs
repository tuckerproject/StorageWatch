/// <summary>
/// Central Server API Models
/// 
/// This file defines the data models used for API communication between agents and the central server.
/// </summary>

using System;

namespace StorageWatch.Models
{
    /// <summary>
    /// Request model for submitting disk space log entries to the central server.
    /// Sent by agents via HTTP POST to the /api/logs/disk-space endpoint.
    /// </summary>
    public class DiskSpaceLogEntry
    {
        /// <summary>
        /// The name of the machine (agent) submitting this data.
        /// </summary>
        public string AgentMachineName { get; set; } = string.Empty;

        /// <summary>
        /// The drive letter being reported (e.g., "C:").
        /// </summary>
        public string DriveLetter { get; set; } = string.Empty;

        /// <summary>
        /// The total space on the drive in gigabytes.
        /// </summary>
        public double TotalSpaceGb { get; set; }

        /// <summary>
        /// The used space on the drive in gigabytes.
        /// </summary>
        public double UsedSpaceGb { get; set; }

        /// <summary>
        /// The free space on the drive in gigabytes.
        /// </summary>
        public double FreeSpaceGb { get; set; }

        /// <summary>
        /// The percentage of free space (0-100).
        /// </summary>
        public double PercentFree { get; set; }

        /// <summary>
        /// The UTC timestamp when the data was collected by the agent.
        /// </summary>
        public DateTime CollectionTimeUtc { get; set; }
    }

    /// <summary>
    /// Response model for central server API responses.
    /// </summary>
    public class ApiResponse
    {
        /// <summary>
        /// Indicates whether the operation was successful.
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// A message providing details about the operation result.
        /// </summary>
        public string Message { get; set; } = string.Empty;

        /// <summary>
        /// Optional data included in the response.
        /// </summary>
        public object? Data { get; set; }
    }
}
