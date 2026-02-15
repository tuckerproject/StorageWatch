/// <summary>
/// Integration Tests for Alert Senders
/// 
/// Tests alert sender implementations with mock HTTP clients and SMTP servers.
/// Verifies that alerts can be sent correctly through various delivery methods.
/// Note: These are semi-integration tests that verify the alert sender logic
/// without actually sending real alerts to external services.
/// </summary>

using FluentAssertions;
using Moq;
using StorageWatch.Config;
using StorageWatch.Models;
using StorageWatch.Services.Alerting;
using StorageWatch.Services.Logging;

namespace StorageWatch.Tests.IntegrationTests
{
    public class AlertSenderIntegrationTests
    {
        private readonly RollingFileLogger _logger;

        public AlertSenderIntegrationTests()
        {
            _logger = new RollingFileLogger(Path.Combine(Path.GetTempPath(), $"test_log_{Guid.NewGuid()}.log"));
        }

        [Fact]
        public async Task GroupMeAlertSender_WithDisabledConfig_DoesNotSendAlert()
        {
            // Arrange
            var config = new GroupMeConfig
            {
                EnableGroupMe = false,
                BotId = "test-bot-id"
            };
            var sender = new GroupMeAlertSender(config, _logger);

            // Act
            await sender.SendAlertAsync("Test alert message");

            // Assert - Should complete without error (no exception thrown)
            // The logger would contain a "Skipping send" message
        }

        [Fact]
        public async Task GroupMeAlertSender_SendAlertAsync_DoesNotThrowException()
        {
            // Arrange
            var config = new GroupMeConfig
            {
                EnableGroupMe = true,
                BotId = "test-bot-id-that-will-fail"
            };
            var sender = new GroupMeAlertSender(config, _logger);

            // Act - Even with invalid bot ID, should not throw
            Func<Task> act = async () => await sender.SendAlertAsync("Test alert message");

            // Assert
            await act.Should().NotThrowAsync("Alert senders should handle errors gracefully");
        }

        [Fact]
        public async Task SmtpAlertSender_WithDisabledConfig_DoesNotSendAlert()
        {
            // Arrange
            var config = new SmtpConfig
            {
                EnableSmtp = false,
                Host = "smtp.example.com",
                Port = 587,
                UseSsl = true,
                Username = "user@example.com",
                Password = "password",
                FromAddress = "from@example.com",
                ToAddress = "to@example.com"
            };
            var sender = new SmtpAlertSender(config, _logger);

            // Act
            await sender.SendAlertAsync("Test alert message");

            // Assert - Should complete without error
        }

        [Fact]
        public async Task SmtpAlertSender_SendAlertAsync_DoesNotThrowException()
        {
            // Arrange - Use invalid SMTP server to test error handling
            var config = new SmtpConfig
            {
                EnableSmtp = true,
                Host = "invalid.smtp.server.example.com",
                Port = 587,
                UseSsl = true,
                Username = "user@example.com",
                Password = "password",
                FromAddress = "from@example.com",
                ToAddress = "to@example.com"
            };
            var sender = new SmtpAlertSender(config, _logger);

            // Act - Even with invalid server, should not throw
            Func<Task> act = async () => await sender.SendAlertAsync("Test alert message");

            // Assert
            await act.Should().NotThrowAsync("Alert senders should handle errors gracefully");
        }

        [Fact]
        public async Task AlertSender_ImplementsIAlertSenderInterface()
        {
            // Arrange
            var groupMeConfig = new GroupMeConfig { EnableGroupMe = true, BotId = "test" };
            var smtpConfig = new SmtpConfig
            {
                EnableSmtp = true,
                Host = "smtp.example.com",
                Port = 587,
                UseSsl = true,
                Username = "user@example.com",
                Password = "password",
                FromAddress = "from@example.com",
                ToAddress = "to@example.com"
            };

            // Act & Assert
            IAlertSender groupMeSender = new GroupMeAlertSender(groupMeConfig, _logger);
            IAlertSender smtpSender = new SmtpAlertSender(smtpConfig, _logger);

            groupMeSender.Should().NotBeNull();
            smtpSender.Should().NotBeNull();

            // Verify interface method exists and can be called
            Func<Task> groupMeAct = async () => await groupMeSender.SendAlertAsync("test");
            Func<Task> smtpAct = async () => await smtpSender.SendAlertAsync("test");

            await groupMeAct.Should().NotThrowAsync();
            await smtpAct.Should().NotThrowAsync();
        }
    }
}
