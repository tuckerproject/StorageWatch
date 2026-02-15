/// <summary>
/// Unit Tests for AlertSenderFactory
/// 
/// Tests the factory logic that creates alert sender instances based on configuration.
/// Verifies that the correct senders are instantiated when enabled in configuration.
/// </summary>

using FluentAssertions;
using Moq;
using StorageWatch.Config.Options;
using StorageWatch.Services.Alerting;
using StorageWatch.Services.Logging;
using StorageWatch.Tests.Utilities;

namespace StorageWatch.Tests.UnitTests
{
    public class AlertSenderFactoryTests
    {
        private readonly Mock<RollingFileLogger> _mockLogger;

        public AlertSenderFactoryTests()
        {
            _mockLogger = new Mock<RollingFileLogger>(Path.GetTempFileName());
        }

        [Fact]
        public void BuildSenders_WithNoSendersEnabled_ReturnsEmptyList()
        {
            // Arrange
            var options = TestHelpers.CreateDefaultTestConfig();
            options.Alerting.GroupMe.Enabled = false;
            options.Alerting.Smtp.Enabled = false;

            // Act
            var senders = AlertSenderFactory.BuildSenders(options, _mockLogger.Object);

            // Assert
            senders.Should().NotBeNull();
            senders.Should().BeEmpty();
        }

        [Fact]
        public void BuildSenders_WithGroupMeEnabled_ReturnsGroupMeSender()
        {
            // Arrange
            var options = TestHelpers.CreateDefaultTestConfig();
            options.Alerting.GroupMe.Enabled = true;
            options.Alerting.GroupMe.BotId = "test-bot-id";
            options.Alerting.Smtp.Enabled = false;

            // Act
            var senders = AlertSenderFactory.BuildSenders(options, _mockLogger.Object);

            // Assert
            senders.Should().NotBeNull();
            senders.Should().HaveCount(1);
            senders[0].Should().BeOfType<GroupMeAlertSender>();
        }

        [Fact]
        public void BuildSenders_WithSmtpEnabled_ReturnsSmtpSender()
        {
            // Arrange
            var options = TestHelpers.CreateDefaultTestConfig();
            options.Alerting.GroupMe.Enabled = false;
            options.Alerting.Smtp.Enabled = true;
            options.Alerting.Smtp.Host = "smtp.example.com";
            options.Alerting.Smtp.Port = 587;
            options.Alerting.Smtp.UseSsl = true;
            options.Alerting.Smtp.Username = "user@example.com";
            options.Alerting.Smtp.Password = "password";
            options.Alerting.Smtp.FromAddress = "from@example.com";
            options.Alerting.Smtp.ToAddress = "to@example.com";

            // Act
            var senders = AlertSenderFactory.BuildSenders(options, _mockLogger.Object);

            // Assert
            senders.Should().NotBeNull();
            senders.Should().HaveCount(1);
            senders[0].Should().BeOfType<SmtpAlertSender>();
        }

        [Fact]
        public void BuildSenders_WithBothEnabled_ReturnsBothSenders()
        {
            // Arrange
            var options = TestHelpers.CreateDefaultTestConfig();
            options.Alerting.GroupMe.Enabled = true;
            options.Alerting.GroupMe.BotId = "test-bot-id";
            options.Alerting.Smtp.Enabled = true;
            options.Alerting.Smtp.Host = "smtp.example.com";
            options.Alerting.Smtp.Port = 587;
            options.Alerting.Smtp.UseSsl = true;
            options.Alerting.Smtp.Username = "user@example.com";
            options.Alerting.Smtp.Password = "password";
            options.Alerting.Smtp.FromAddress = "from@example.com";
            options.Alerting.Smtp.ToAddress = "to@example.com";

            // Act
            var senders = AlertSenderFactory.BuildSenders(options, _mockLogger.Object);

            // Assert
            senders.Should().NotBeNull();
            senders.Should().HaveCount(2);
            senders.Should().ContainSingle(s => s is GroupMeAlertSender);
            senders.Should().ContainSingle(s => s is SmtpAlertSender);
        }

        [Fact]
        public void BuildSenders_WithNullGroupMeConfig_DoesNotAddGroupMeSender()
        {
            // Arrange
            var options = TestHelpers.CreateDefaultTestConfig();
            options.Alerting.GroupMe.Enabled = false;
            options.Alerting.Smtp.Enabled = false;
            options.Alerting.GroupMe = null!;

            // Act
            var senders = AlertSenderFactory.BuildSenders(options, _mockLogger.Object);

            // Assert
            senders.Should().BeEmpty();
        }

        [Fact]
        public void BuildSenders_WithNullSmtpConfig_DoesNotAddSmtpSender()
        {
            // Arrange
            var options = TestHelpers.CreateDefaultTestConfig();
            options.Alerting.GroupMe.Enabled = false;
            options.Alerting.Smtp.Enabled = false;
            options.Alerting.Smtp = null!;

            // Act
            var senders = AlertSenderFactory.BuildSenders(options, _mockLogger.Object);

            // Assert
            senders.Should().BeEmpty();
        }
    }
}
