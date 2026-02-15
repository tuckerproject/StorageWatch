/// <summary>
/// Unit Tests for AlertSenderFactory and Plugin Architecture
/// 
/// Tests the factory logic and plugin discovery/filtering that creates alert sender instances
/// based on configuration. Verifies that the correct senders are instantiated when enabled
/// in configuration and that the plugin architecture works correctly.
/// </summary>

using FluentAssertions;
using Moq;
using StorageWatch.Config.Options;
using StorageWatch.Models;
using StorageWatch.Services.Alerting;
using StorageWatch.Services.Alerting.Plugins;
using StorageWatch.Services.Logging;
using StorageWatch.Tests.Utilities;
using Microsoft.Extensions.DependencyInjection;

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
#pragma warning disable CS0618 // Type or member is obsolete
            var senders = AlertSenderFactory.BuildSenders(options, _mockLogger.Object);
#pragma warning restore CS0618

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
#pragma warning disable CS0618 // Type or member is obsolete
            var senders = AlertSenderFactory.BuildSenders(options, _mockLogger.Object);
#pragma warning restore CS0618

            // Assert
            senders.Should().NotBeNull();
            senders.Should().HaveCount(1);
            senders[0].Should().BeOfType<GroupMeAlertSender>();
            senders[0].Name.Should().Be("GroupMe");
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
#pragma warning disable CS0618 // Type or member is obsolete
            var senders = AlertSenderFactory.BuildSenders(options, _mockLogger.Object);
#pragma warning restore CS0618

            // Assert
            senders.Should().NotBeNull();
            senders.Should().HaveCount(1);
            senders[0].Should().BeOfType<SmtpAlertSender>();
            senders[0].Name.Should().Be("SMTP");
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
#pragma warning disable CS0618 // Type or member is obsolete
            var senders = AlertSenderFactory.BuildSenders(options, _mockLogger.Object);
#pragma warning restore CS0618

            // Assert
            senders.Should().NotBeNull();
            senders.Should().HaveCount(2);
        }

        [Fact]
        public void PluginRegistry_DiscoverPlugins_FindsBuiltInPlugins()
        {
            // Arrange
            var registry = new AlertSenderPluginRegistry();

            // Act
            registry.DiscoverPlugins();

            // Assert
            registry.Plugins.Should().NotBeEmpty();
            registry.IsPluginRegistered("SMTP").Should().BeTrue();
            registry.IsPluginRegistered("GroupMe").Should().BeTrue();
        }

        [Fact]
        public void PluginRegistry_GetPlugin_ReturnsCorrectMetadata()
        {
            // Arrange
            var registry = new AlertSenderPluginRegistry();
            registry.DiscoverPlugins();

            // Act
            var smtpPlugin = registry.GetPlugin("SMTP");

            // Assert
            smtpPlugin.Should().NotBeNull();
            smtpPlugin!.PluginId.Should().Be("SMTP");
            smtpPlugin.IsBuiltIn.Should().BeTrue();
        }

        [Fact]
        public void PluginManager_GetEnabledSenders_ReturnsOnlyEnabledPlugins()
        {
            // Arrange
            var options = TestHelpers.CreateDefaultTestConfig();
            options.Alerting.EnableNotifications = true;
            options.Alerting.Smtp.Enabled = true;
            options.Alerting.Smtp.Host = "smtp.example.com";
            options.Alerting.Smtp.Port = 587;
            options.Alerting.Smtp.FromAddress = "from@example.com";
            options.Alerting.Smtp.ToAddress = "to@example.com";
            options.Alerting.GroupMe.Enabled = false;

            var services = new ServiceCollection();
            services.AddTransient<IAlertSender>(sp => new SmtpAlertSender(options.Alerting.Smtp, _mockLogger.Object));
            services.AddTransient<IAlertSender>(sp => new GroupMeAlertSender(options.Alerting.GroupMe, _mockLogger.Object));
            var serviceProvider = services.BuildServiceProvider();

            var registry = new AlertSenderPluginRegistry();
            registry.DiscoverPlugins();

            var manager = new AlertSenderPluginManager(serviceProvider, options, _mockLogger.Object, registry);

            // Act
            var enabledSenders = manager.GetEnabledSenders();

            // Assert
            enabledSenders.Should().HaveCount(1);
            enabledSenders[0].Name.Should().Be("SMTP");
        }

        [Fact]
        public void PluginManager_GetEnabledSenders_ReturnsEmpty_WhenNotificationsDisabled()
        {
            // Arrange
            var options = TestHelpers.CreateDefaultTestConfig();
            options.Alerting.EnableNotifications = false; // Notifications disabled globally
            options.Alerting.Smtp.Enabled = true;
            options.Alerting.GroupMe.Enabled = true;

            var services = new ServiceCollection();
            services.AddTransient<IAlertSender>(sp => new SmtpAlertSender(options.Alerting.Smtp, _mockLogger.Object));
            services.AddTransient<IAlertSender>(sp => new GroupMeAlertSender(options.Alerting.GroupMe, _mockLogger.Object));
            var serviceProvider = services.BuildServiceProvider();

            var registry = new AlertSenderPluginRegistry();
            registry.DiscoverPlugins();

            var manager = new AlertSenderPluginManager(serviceProvider, options, _mockLogger.Object, registry);

            // Act
            var enabledSenders = manager.GetEnabledSenders();

            // Assert
            enabledSenders.Should().BeEmpty();
        }
    }
}
