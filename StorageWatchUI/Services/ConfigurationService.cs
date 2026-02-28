using Microsoft.Extensions.Configuration;
using System.Diagnostics;
using System.IO;
using System.Text.Json;

namespace StorageWatchUI.Services;

/// <summary>
/// Service for reading and managing StorageWatch Agent configuration.
/// </summary>
public class ConfigurationService
{
    private readonly IConfiguration _configuration;
    private string? _configPath;

    public ConfigurationService(IConfiguration configuration)
    {
        _configuration = configuration;
        _configPath = FindConfigPath();
    }

    private string? FindConfigPath()
    {
        var programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
        var path = Path.Combine(programData, "StorageWatch", "Agent", "AgentConfig.json");

        if (File.Exists(path))
            return path;

        return null;
    }

    /// <summary>
    /// Gets the full configuration as a formatted JSON string for display.
    /// </summary>
    public async Task<string> GetConfigurationAsJsonAsync()
    {
        if (string.IsNullOrEmpty(_configPath) || !File.Exists(_configPath))
        {
            return "Configuration file not found.";
        }

        try
        {
            var json = await File.ReadAllTextAsync(_configPath);
            var doc = JsonDocument.Parse(json);
            return JsonSerializer.Serialize(doc, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            return $"Error reading configuration: {ex.Message}";
        }
    }

    /// <summary>
    /// Opens the configuration file in Notepad.
    /// </summary>
    public void OpenConfigInNotepad()
    {
        if (string.IsNullOrEmpty(_configPath) || !File.Exists(_configPath))
        {
            throw new FileNotFoundException("Configuration file not found.");
        }

        Process.Start("notepad.exe", _configPath);
    }

    /// <summary>
    /// Gets the path to the configuration file.
    /// </summary>
    public string? GetConfigPath() => _configPath;

    /// <summary>
    /// Gets the threshold percent from configuration.
    /// </summary>
    public double GetThresholdPercent()
    {
        return _configuration.GetValue<double>("StorageWatch:Monitoring:ThresholdPercent", 10.0);
    }
}
