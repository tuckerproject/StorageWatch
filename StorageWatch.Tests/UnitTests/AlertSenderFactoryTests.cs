/// <summary>
/// Unit Tests for AlertSenderFactory
/// 
/// Tests the factory logic that creates alert sender instances based on configuration.
/// Verifies that the correct senders are instantiated when enabled in configuration.
/// </summary>

using FluentAssertions;
using Moq;
using StorageWatch.Config;
using StorageWatch.Services.Alerting;
using StorageWatch.Services.Logging;

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
            var config = new StorageWatchConfig
            {
                GroupMe = new GroupMeConfig { EnableGroupMe = false },
                Smtp = new SmtpConfig { EnableSmtp = false }
            };

            // Act
            var senders = AlertSenderFactory.BuildSenders(config, _mockLogger.Object);

            // Assert
            senders.Should().NotBeNull();
            senders.Should().BeEmpty();
        }

        [Fact]
        public void BuildSenders_WithGroupMeEnabled_ReturnsGroupMeSender()
        {
            // Arrange
            var config = new StorageWatchConfig
            {
                GroupMe = new GroupMeConfig
                {
                    EnableGroupMe = true,
                    BotId = "test-bot-id"
                },
                Smtp = new SmtpConfig { EnableSmtp = false }
            };

            // Act
            var senders = AlertSenderFactory.BuildSenders(config, _mockLogger.Object);

            // Assert
            senders.Should().NotBeNull();
            senders.Should().HaveCount(1);
            senders[0].Should().BeOfType<GroupMeAlertSender>();
        }

        [Fact]
        public void BuildSenders_WithSmtpEnabled_ReturnsSmtpSender()
        {
            // Arrange
            var config = new StorageWatchConfig
            {
                GroupMe = new GroupMeConfig { EnableGroupMe = false },
                Smtp = new SmtpConfig
                {
                    EnableSmtp = true,
                    Host = "smtp.example.com",
                    Port = 587,
                    UseSsl = true,
                    Username = "user@example.com",
                    Password = "password",
                    FromAddress = "from@example.com",
                    ToAddress = "to@example.com"
                }
            };

            // Act
            var senders = AlertSenderFactory.BuildSenders(config, _mockLogger.Object);

            // Assert
            senders.Should().NotBeNull();
            senders.Should().HaveCount(1);
            senders[0].Should().BeOfType<SmtpAlertSender>();
        }

        [Fact]
        public void BuildSenders_WithBothEnabled_ReturnsBothSenders()
        {
            // Arrange
            var config = new StorageWatchConfig
            {
                GroupMe = new GroupMeConfig
                {
                    EnableGroupMe = true,
                    BotId = "test-bot-id"
                },
                Smtp = new SmtpConfig
                {
                    EnableSmtp = true,
                    Host = "smtp.example.com",
                    Port = 587,
                    UseSsl = true,
                    Username = "user@example.com",
                    Password = "password",
                    FromAddress = "from@example.com",
                    ToAddress = "to@example.com"
                }
            };

            // Act
            var senders = AlertSenderFactory.BuildSenders(config, _mockLogger.Object);

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
            var config = new StorageWatchConfig
            {
                GroupMe = null!,
                Smtp = new SmtpConfig { EnableSmtp = false }
            };

            // Act
            var senders = AlertSenderFactory.BuildSenders(config, _mockLogger.Object);

            // Assert
            senders.Should().BeEmpty();
        }

        [Fact]
        public void BuildSenders_WithNullSmtpConfig_DoesNotAddSmtpSender()
        {
            // Arrange
            var config = new StorageWatchConfig
            {
                GroupMe = new GroupMeConfig { EnableGroupMe = false },
                Smtp = null!
            };

            // Act
            var senders = AlertSenderFactory.BuildSenders(config, _mockLogger.Object);

            // Assert
            senders.Should().BeEmpty();
        }
    }
}
