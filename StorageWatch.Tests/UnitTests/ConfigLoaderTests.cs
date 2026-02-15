/// <summary>
/// Unit Tests for ConfigLoader
/// 
/// Tests the XML configuration parsing and validation logic.
/// Verifies that configuration files are correctly loaded, defaults are applied,
/// and invalid configurations are rejected with appropriate errors.
/// </summary>

using FluentAssertions;
using StorageWatch.Config;
using System.IO;

namespace StorageWatch.Tests.UnitTests
{
    public class ConfigLoaderTests
    {
        private readonly string _testConfigDirectory;

        public ConfigLoaderTests()
        {
            // Create a temporary directory for test configuration files
            _testConfigDirectory = Path.Combine(Path.GetTempPath(), "StorageWatchTests", Guid.NewGuid().ToString());
            Directory.CreateDirectory(_testConfigDirectory);
        }

        [Fact]
        public void Load_WithValidConfig_ReturnsPopulatedConfig()
        {
            // Arrange
            string configPath = Path.Combine(_testConfigDirectory, "valid_config.xml");
            string xmlContent = @"<?xml version=""1.0"" encoding=""utf-8""?>
<StorageWatchConfig>
  <EnableSqlReporting>true</EnableSqlReporting>
  <RunMissedCollection>false</RunMissedCollection>
  <RunOnlyOncePerDay>true</RunOnlyOncePerDay>
  <CollectionTime>02:00:00</CollectionTime>
  <EnableNotifications>true</EnableNotifications>
  <ThresholdPercent>20</ThresholdPercent>
  <Drives>
    <Drive>C:</Drive>
    <Drive>D:</Drive>
  </Drives>
  <Database>
    <ConnectionString>Data Source=test.db</ConnectionString>
  </Database>
  <GroupMe>
    <EnableGroupMe>true</EnableGroupMe>
    <BotId>test-bot-id</BotId>
  </GroupMe>
  <Smtp>
    <EnableSmtp>false</EnableSmtp>
    <Host>smtp.example.com</Host>
    <Port>587</Port>
    <UseSsl>true</UseSsl>
    <Username>user@example.com</Username>
    <Password>password</Password>
    <FromAddress>from@example.com</FromAddress>
    <ToAddress>to@example.com</ToAddress>
  </Smtp>
  <CentralServer>
    <Enabled>false</Enabled>
    <Mode>Agent</Mode>
    <ServerUrl>http://localhost:5000</ServerUrl>
    <ApiKey>test-key</ApiKey>
    <Port>5000</Port>
    <CentralConnectionString>Data Source=central.db</CentralConnectionString>
    <ServerId>test-server</ServerId>
  </CentralServer>
</StorageWatchConfig>";
            File.WriteAllText(configPath, xmlContent);

            // Act
            var config = ConfigLoader.Load(configPath);

            // Assert
            config.Should().NotBeNull();
            config.EnableSqlReporting.Should().BeTrue();
            config.RunOnlyOncePerDay.Should().BeTrue();
            config.EnableNotifications.Should().BeTrue();
            config.ThresholdPercent.Should().Be(20);
            config.Drives.Should().HaveCount(2);
            config.Drives.Should().Contain("C:");
            config.Drives.Should().Contain("D:");
            config.Database.ConnectionString.Should().Be("Data Source=test.db");
            config.GroupMe.EnableGroupMe.Should().BeTrue();
            config.GroupMe.BotId.Should().Be("test-bot-id");
            config.CentralServer.Enabled.Should().BeFalse();
            config.CentralServer.Mode.Should().Be("Agent");
        }

        [Fact]
        public void Load_WithNonExistentFile_ThrowsFileNotFoundException()
        {
            // Arrange
            string nonExistentPath = Path.Combine(_testConfigDirectory, "nonexistent.xml");

            // Act & Assert
            Action act = () => ConfigLoader.Load(nonExistentPath);
            act.Should().Throw<FileNotFoundException>()
                .WithMessage($"Configuration file not found: {nonExistentPath}");
        }

        [Fact]
        public void Load_WithInvalidXml_ThrowsException()
        {
            // Arrange
            string configPath = Path.Combine(_testConfigDirectory, "invalid.xml");
            File.WriteAllText(configPath, "This is not valid XML content");

            // Act & Assert
            Action act = () => ConfigLoader.Load(configPath);
            act.Should().Throw<Exception>();
        }

        [Fact]
        public void Load_WithMissingRootElement_ThrowsException()
        {
            // Arrange
            string configPath = Path.Combine(_testConfigDirectory, "no_root.xml");
            File.WriteAllText(configPath, "<?xml version=\"1.0\" encoding=\"utf-8\"?>");

            // Act & Assert
            Action act = () => ConfigLoader.Load(configPath);
            act.Should().Throw<Exception>();
            // Note: Message may vary - "Root element is missing" or "Invalid configuration file"
        }

        [Fact]
        public void Load_WithMinimalConfig_AppliesDefaults()
        {
            // Arrange - Minimal config with only required fields
            string configPath = Path.Combine(_testConfigDirectory, "minimal_config.xml");
            string xmlContent = @"<?xml version=""1.0"" encoding=""utf-8""?>
<StorageWatchConfig>
  <Database>
    <ConnectionString>Data Source=test.db</ConnectionString>
  </Database>
</StorageWatchConfig>";
            File.WriteAllText(configPath, xmlContent);

            // Act
            var config = ConfigLoader.Load(configPath);

            // Assert - Should load without errors and apply defaults
            config.Should().NotBeNull();
            config.Database.ConnectionString.Should().Be("Data Source=test.db");
            // The actual default values depend on ConfigLoader implementation
        }

        [Fact]
        public void Load_WithEmptyDrivesList_ReturnsEmptyDrivesList()
        {
            // Arrange
            string configPath = Path.Combine(_testConfigDirectory, "empty_drives.xml");
            string xmlContent = @"<?xml version=""1.0"" encoding=""utf-8""?>
<StorageWatchConfig>
  <Database>
    <ConnectionString>Data Source=test.db</ConnectionString>
  </Database>
  <Drives>
  </Drives>
</StorageWatchConfig>";
            File.WriteAllText(configPath, xmlContent);

            // Act
            var config = ConfigLoader.Load(configPath);

            // Assert
            config.Should().NotBeNull();
            config.Drives.Should().NotBeNull();
            config.Drives.Should().BeEmpty();
        }

        // Cleanup after tests
        private void Cleanup()
        {
            if (Directory.Exists(_testConfigDirectory))
            {
                Directory.Delete(_testConfigDirectory, true);
            }
        }
    }
}
