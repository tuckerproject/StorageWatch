using FluentAssertions;
using Microsoft.Extensions.Configuration;
using StorageWatchUI.Services;
using System.IO;
using System.Text.Json;
using Xunit;

namespace StorageWatchUI.Tests.Services;

public class ConfigurationServiceTests : IDisposable
{
    private readonly string _testProgramDataPath;
    private readonly string _testCurrentDirPath;
    private readonly string _originalCurrentDir;

    public ConfigurationServiceTests()
    {
        _originalCurrentDir = Directory.GetCurrentDirectory();

        // Setup test directories
        _testProgramDataPath = Path.Combine(Path.GetTempPath(), $"StorageWatch_Test_{Guid.NewGuid()}");
        _testCurrentDirPath = Path.Combine(Path.GetTempPath(), $"StorageWatch_CurrentDir_{Guid.NewGuid()}");

        Directory.CreateDirectory(_testProgramDataPath);
        Directory.CreateDirectory(_testCurrentDirPath);
    }

    [Fact]
    public async Task GetConfigurationAsJsonAsync_WithValidConfig_ReturnsFormattedJson()
    {
        // Arrange
        var configPath = Path.Combine(_testCurrentDirPath, "StorageWatchConfig.json");
        var testConfig = new
        {
            StorageWatch = new
            {
                Monitoring = new { ThresholdPercent = 10 },
                Database = new { ConnectionString = "Data Source=test.db" }
            }
        };

        await File.WriteAllTextAsync(configPath, JsonSerializer.Serialize(testConfig));
        Directory.SetCurrentDirectory(_testCurrentDirPath);

        var config = new ConfigurationBuilder().Build();
        var service = new ConfigurationService(config);

        // Act
        var result = await service.GetConfigurationAsJsonAsync();

        // Assert
        result.Should().Contain("StorageWatch");
        result.Should().Contain("Monitoring");
        result.Should().NotContain("Configuration file not found");
    }

    [Fact]
    public async Task GetConfigurationAsJsonAsync_WithMissingConfig_ReturnsErrorMessage()
    {
        // Arrange
        Directory.SetCurrentDirectory(_testCurrentDirPath); // Empty directory
        var config = new ConfigurationBuilder().Build();
        var service = new ConfigurationService(config);

        // Act
        var result = await service.GetConfigurationAsJsonAsync();

        // Assert
        result.Should().Contain("Configuration file not found");
    }

    [Fact]
    public async Task GetConfigurationAsJsonAsync_WithCorruptedJson_ReturnsErrorMessage()
    {
        // Arrange
        var configPath = Path.Combine(_testCurrentDirPath, "StorageWatchConfig.json");
        await File.WriteAllTextAsync(configPath, "{ invalid json content }}}");
        Directory.SetCurrentDirectory(_testCurrentDirPath);

        var config = new ConfigurationBuilder().Build();
        var service = new ConfigurationService(config);

        // Act
        var result = await service.GetConfigurationAsJsonAsync();

        // Assert
        result.Should().Contain("Error reading configuration");
    }

    [Fact]
    public void GetConfigPath_WithConfigInCurrentDirectory_ReturnsPath()
    {
        // Arrange
        var configPath = Path.Combine(_testCurrentDirPath, "StorageWatchConfig.json");
        File.WriteAllText(configPath, "{}");
        Directory.SetCurrentDirectory(_testCurrentDirPath);

        var config = new ConfigurationBuilder().Build();
        var service = new ConfigurationService(config);

        // Act
        var result = service.GetConfigPath();

        // Assert
        result.Should().NotBeNull();
        result.Should().EndWith("StorageWatchConfig.json");
        File.Exists(result).Should().BeTrue();
    }

    [Fact]
    public void GetConfigPath_WithNoConfig_ReturnsNull()
    {
        // Arrange
        Directory.SetCurrentDirectory(_testCurrentDirPath); // Empty directory
        var config = new ConfigurationBuilder().Build();
        var service = new ConfigurationService(config);

        // Act
        var result = service.GetConfigPath();

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void IsCentralServerEnabled_WithEnabledConfig_ReturnsTrue()
    {
        // Arrange
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "StorageWatch:CentralServer:Enabled", "true" }
            })
            .Build();

        var service = new ConfigurationService(config);

        // Act
        var result = service.IsCentralServerEnabled();

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void IsCentralServerEnabled_WithDisabledConfig_ReturnsFalse()
    {
        // Arrange
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "StorageWatch:CentralServer:Enabled", "false" }
            })
            .Build();

        var service = new ConfigurationService(config);

        // Act
        var result = service.IsCentralServerEnabled();

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void GetThresholdPercent_WithConfigValue_ReturnsConfiguredValue()
    {
        // Arrange
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "StorageWatch:Monitoring:ThresholdPercent", "15.5" }
            })
            .Build();

        var service = new ConfigurationService(config);

        // Act
        var result = service.GetThresholdPercent();

        // Assert
        result.Should().Be(15.5);
    }

    [Fact]
    public void GetThresholdPercent_WithMissingConfig_ReturnsDefaultValue()
    {
        // Arrange
        var config = new ConfigurationBuilder().Build();
        var service = new ConfigurationService(config);

        // Act
        var result = service.GetThresholdPercent();

        // Assert
        result.Should().Be(10.0); // Default value
    }

    [Fact]
    public void OpenConfigInNotepad_WithMissingConfig_ThrowsFileNotFoundException()
    {
        // Arrange
        Directory.SetCurrentDirectory(_testCurrentDirPath);
        var config = new ConfigurationBuilder().Build();
        var service = new ConfigurationService(config);

        // Act & Assert
        var act = () => service.OpenConfigInNotepad();
        act.Should().Throw<FileNotFoundException>();
    }

    public void Dispose()
    {
        // Restore original directory
        Directory.SetCurrentDirectory(_originalCurrentDir);

        // Cleanup test directories
        if (Directory.Exists(_testProgramDataPath))
        {
            Directory.Delete(_testProgramDataPath, true);
        }

        if (Directory.Exists(_testCurrentDirPath))
        {
            Directory.Delete(_testCurrentDirPath, true);
        }
    }
}
