/// <summary>
/// Unit Tests for DiskAlertMonitor
/// 
/// Tests the drive scanning logic to ensure accurate disk space metrics are returned.
/// These tests verify behavior for ready drives, not-ready drives, and error conditions.
/// </summary>

using FluentAssertions;
using StorageWatch.Config.Options;
using StorageWatch.Services.Monitoring;
using StorageWatch.Tests.Utilities;
using System.IO;

namespace StorageWatch.Tests.UnitTests
{
    public class DiskAlertMonitorTests
    {
        [Fact]
        public void GetStatus_WithReadyDrive_ReturnsValidStatus()
        {
            // Arrange
            var options = TestHelpers.CreateDefaultTestConfig();
            var monitor = new DiskAlertMonitor(options);

            // Act
            var status = monitor.GetStatus("C:");

            // Assert
            status.Should().NotBeNull();
            status.DriveName.Should().Be("C:");
            status.TotalSpaceGb.Should().BeGreaterThan(0);
            status.FreeSpaceGb.Should().BeGreaterOrEqualTo(0);
            status.FreeSpaceGb.Should().BeLessOrEqualTo(status.TotalSpaceGb);
        }

        [Fact]
        public void GetStatus_WithInvalidDrive_ReturnsZeroValues()
        {
            // Arrange
            var options = TestHelpers.CreateDefaultTestConfig();
            var monitor = new DiskAlertMonitor(options);

            // Act
            var status = monitor.GetStatus("Z:");

            // Assert
            status.Should().NotBeNull();
            status.DriveName.Should().Be("Z:");
            status.TotalSpaceGb.Should().Be(0);
            status.FreeSpaceGb.Should().Be(0);
            status.PercentFree.Should().Be(0);
        }

        [Fact]
        public void GetStatus_CalculatesPercentFreeCorrectly()
        {
            // Arrange
            var options = TestHelpers.CreateDefaultTestConfig();
            var monitor = new DiskAlertMonitor(options);

            // Act
            var status = monitor.GetStatus("C:");

            // Assert - PercentFree should be between 0 and 100
            if (status.TotalSpaceGb > 0)
            {
                status.PercentFree.Should().BeInRange(0, 100);
                var expectedPercent = Math.Round((status.FreeSpaceGb / status.TotalSpaceGb) * 100, 2);
                status.PercentFree.Should().Be(expectedPercent);
            }
        }

        [Fact]
        public void GetStatus_WithNullOrEmptyDriveLetter_HandlesGracefully()
        {
            // Arrange
            var options = TestHelpers.CreateDefaultTestConfig();
            var monitor = new DiskAlertMonitor(options);

            // Act
            var statusEmpty = monitor.GetStatus("");
            var statusNull = monitor.GetStatus(null!);

            // Assert - Should return safe zero values instead of throwing
            statusEmpty.TotalSpaceGb.Should().Be(0);
            statusNull.TotalSpaceGb.Should().Be(0);
        }
    }
}
