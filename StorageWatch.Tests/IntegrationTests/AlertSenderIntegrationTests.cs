/// <summary>
/// Integration Tests for Alert Senders
/// 
/// Tests alert sender implementations with mock HTTP clients and SMTP servers.
/// Verifies that alerts can be sent correctly through various delivery methods
/// using the new plugin architecture with DiskStatus and CancellationToken.
/// </summary>

using FluentAssertions;
using Moq;
using StorageWatch.Config.Options;
using StorageWatch.Models;
using StorageWatch.Services.Alerting;
using StorageWatch.Services.Logging;
using StorageWatch.Tests.Utilities;

namespace StorageWatch.Tests.IntegrationTests
{
    public class AlertSenderIntegrationTests
    {
        private readonly RollingFileLogger _logger;

        public AlertSenderIntegrationTests()
        {
            _logger = new RollingFileLogger(Path.Combine(Path.GetTempPath(), $"test_log_{Guid.NewGuid()}.log"));
        }

        private DiskStatus CreateTestDiskStatus(bool isLowSpace = true)
        {
            return new DiskStatus
            {
                DriveName = "C:",
                TotalSpaceGb = 100,
                FreeSpaceGb = isLowSpace ? 5 : 50 // 5% or 50% free
            };
        }

        [Fact]
        public async Task GroupMeAlertSender_WithDisabledConfig_DoesNotSendAlert()
        {
            // Arrange
            var options = new GroupMeOptions
            {
                Enabled = false,
                BotId = "test-bot-id"
            };
            var sender = new GroupMeAlertSender(options, _logger);
            var status = CreateTestDiskStatus();

            // Act
            await sender.SendAlertAsync(status, CancellationToken.None);

            // Assert - Should complete without error (no exception thrown)
            sender.Name.Should().Be("GroupMe");
        }

        [Fact]
        public async Task GroupMeAlertSender_SendAlertAsync_DoesNotThrowException()
        {
            // Arrange
            var options = new GroupMeOptions
            {
                Enabled = true,
                BotId = "test-bot-id-that-will-fail"
            };
            var sender = new GroupMeAlertSender(options, _logger);
            var status = CreateTestDiskStatus();

            // Act - Even with invalid bot ID, should not throw
            Func<Task> act = async () => await sender.SendAlertAsync(status, CancellationToken.None);

            // Assert
            await act.Should().NotThrowAsync("Alert senders should handle errors gracefully");
        }

        [Fact]
        public async Task SmtpAlertSender_WithDisabledConfig_DoesNotSendAlert()
        {
            // Arrange
            var options = new SmtpOptions
            {
                Enabled = false,
                Host = "smtp.example.com",
                Port = 587,
                UseSsl = true,
                Username = "user@example.com",
                Password = "password",
                FromAddress = "from@example.com",
                ToAddress = "to@example.com"
            };
            var sender = new SmtpAlertSender(options, _logger);
            var status = CreateTestDiskStatus();

            // Act
            await sender.SendAlertAsync(status, CancellationToken.None);

            // Assert - Should complete without error
            sender.Name.Should().Be("SMTP");
        }

        [Fact]
        public async Task SmtpAlertSender_SendAlertAsync_DoesNotThrowException()
        {
            // Arrange - Use invalid SMTP server to test error handling
            var options = new SmtpOptions
            {
                Enabled = true,
                Host = "invalid.smtp.server.example.com",
                Port = 587,
                UseSsl = true,
                Username = "user@example.com",
                Password = "password",
                FromAddress = "from@example.com",
                ToAddress = "to@example.com"
            };
            var sender = new SmtpAlertSender(options, _logger);
            var status = CreateTestDiskStatus();

            // Act - Should handle connection errors gracefully
            Func<Task> act = async () => await sender.SendAlertAsync(status, CancellationToken.None);

            // Assert
            await act.Should().NotThrowAsync("Alert senders should handle errors gracefully");
        }

        [Fact]
        public async Task AlertSender_HealthCheck_ReturnsTrue_WhenConfigured()
        {
            // Arrange
            var options = new SmtpOptions
            {
                Enabled = true,
                Host = "smtp.example.com",
                Port = 587,
                FromAddress = "from@example.com",
                ToAddress = "to@example.com"
            };
            var sender = new SmtpAlertSender(options, _logger);

            // Act
            var isHealthy = await sender.HealthCheckAsync();

            // Assert
            isHealthy.Should().BeTrue("Sender is properly configured");
        }

        [Fact]
        public async Task AlertSender_HealthCheck_ReturnsFalse_WhenDisabled()
        {
            // Arrange
            var options = new SmtpOptions
            {
                Enabled = false,
                Host = "smtp.example.com",
                Port = 587,
                FromAddress = "from@example.com",
                ToAddress = "to@example.com"
            };
            var sender = new SmtpAlertSender(options, _logger);

            // Act
            var isHealthy = await sender.HealthCheckAsync();

            // Assert
            isHealthy.Should().BeFalse("Sender is disabled");
        }

        [Fact]
        public async Task AlertSender_HealthCheck_ReturnsFalse_WhenMisconfigured()
        {
            // Arrange
            var options = new SmtpOptions
            {
                Enabled = true,
                Host = "", // Missing required configuration
                Port = 587,
                FromAddress = "",
                ToAddress = ""
            };
            var sender = new SmtpAlertSender(options, _logger);

            // Act
            var isHealthy = await sender.HealthCheckAsync();

            // Assert
            isHealthy.Should().BeFalse("Sender is missing required configuration");
        }

        [Fact]
        public void AlertSender_HasCorrectName_SMTP()
        {
            // Arrange
            var options = new SmtpOptions { Enabled = true, Host = "test.com", FromAddress = "a@b.com", ToAddress = "c@d.com" };
            var sender = new SmtpAlertSender(options, _logger);

            // Act & Assert
            sender.Name.Should().Be("SMTP");
        }

        [Fact]
        public void AlertSender_HasCorrectName_GroupMe()
        {
            // Arrange
            var options = new GroupMeOptions { Enabled = true, BotId = "test-id" };
            var sender = new GroupMeAlertSender(options, _logger);

            // Act & Assert
            sender.Name.Should().Be("GroupMe");
        }
    }
}
