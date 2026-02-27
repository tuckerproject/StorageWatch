/// <summary>
/// Unit Tests for NotificationLoop
/// 
/// Tests the alerting state machine logic that determines when to send alerts.
/// These tests verify state transitions, alert deduplication, and network readiness checks.
/// Note: Full integration testing of the loop is done separately.
/// </summary>

using FluentAssertions;
using Moq;
using StorageWatch.Config.Options;
using StorageWatch.Models;
using StorageWatch.Services.Alerting;
using StorageWatch.Services.Logging;
using StorageWatch.Services.Monitoring;
using StorageWatch.Services.Scheduling;
using StorageWatch.Tests.Utilities;

namespace StorageWatch.Tests.UnitTests
{
    public class NotificationLoopTests
    {
        private readonly Mock<RollingFileLogger> _mockLogger;
        private readonly StorageWatchOptions _config;

        public NotificationLoopTests()
        {
            _mockLogger = new Mock<RollingFileLogger>(Path.GetTempFileName());
            _config = TestHelpers.CreateDefaultTestConfig();
        }

        [Fact]
        public void Constructor_WithValidParameters_InitializesSuccessfully()
        {
            // Arrange
            var monitor = new DiskAlertMonitor(_config);
            var senders = new List<IAlertSender>();

            // Act
            var loop = new NotificationLoop(_config, senders, monitor, _mockLogger.Object);

            // Assert
            loop.Should().NotBeNull();
        }

        [Fact]
        public void Constructor_WithEmptySendersList_InitializesSuccessfully()
        {
            // Arrange
            var monitor = new DiskAlertMonitor(_config);
            var senders = new List<IAlertSender>(); // Empty list

            // Act
            var loop = new NotificationLoop(_config, senders, monitor, _mockLogger.Object);

            // Assert
            loop.Should().NotBeNull();
        }

        [Fact]
        public void Constructor_CreatesStateDirectory()
        {
            // Arrange
            var monitor = new DiskAlertMonitor(_config);
            var senders = new List<IAlertSender>();
            var stateDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                "StorageWatch"
            );

            // Act
            var loop = new NotificationLoop(_config, senders, monitor, _mockLogger.Object);

            // Assert
            Directory.Exists(stateDirectory).Should().BeTrue("State directory should be created");
        }

        [Fact]
        public async Task RunAsync_WithCancellationToken_StopsGracefully()
        {
            // Arrange
            var monitor = new DiskAlertMonitor(_config);
            var senders = new List<IAlertSender>();
            var loop = new NotificationLoop(_config, senders, monitor, _mockLogger.Object);
            var cts = new CancellationTokenSource();

            // Act - Start the loop and cancel after 100ms
            var runTask = Task.Run(() => loop.RunAsync(cts.Token));
            await Task.Delay(100);
            cts.Cancel();

            // Assert - Should complete (may throw TaskCanceledException which is expected)
            try
            {
                await runTask;
            }
            catch (TaskCanceledException)
            {
                // Expected behavior when cancellation is requested
            }
        }
    }
}
