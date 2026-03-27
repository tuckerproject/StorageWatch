using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Text.Json;

namespace StorageWatchUI.Services.AutoUpdate
{
    public interface IUiUpdateUserSettingsStore
    {
        string? GetSkippedVersion();
        void SetSkippedVersion(string? version);
    }

    internal sealed class UiUpdateUserSettingsStore : IUiUpdateUserSettingsStore
    {
        private static readonly JsonSerializerOptions SerializerOptions = new() { WriteIndented = true };
        private readonly ILogger<UiUpdateUserSettingsStore> _logger;
        private readonly string _settingsPath;

        public UiUpdateUserSettingsStore(ILogger<UiUpdateUserSettingsStore> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            _settingsPath = Path.Combine(appData, "StorageWatch", "UI", "user-settings.json");
        }

        public string? GetSkippedVersion()
        {
            try
            {
                if (!File.Exists(_settingsPath))
                    return null;

                var json = File.ReadAllText(_settingsPath);
                var settings = JsonSerializer.Deserialize<UiUpdateUserSettings>(json);
                return string.IsNullOrWhiteSpace(settings?.SkippedVersion) ? null : settings!.SkippedVersion;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[AUTOUPDATE] Failed to read UI update user settings.");
                return null;
            }
        }

        public void SetSkippedVersion(string? version)
        {
            try
            {
                var directory = Path.GetDirectoryName(_settingsPath);
                if (!string.IsNullOrWhiteSpace(directory))
                    Directory.CreateDirectory(directory);

                var settings = new UiUpdateUserSettings
                {
                    SkippedVersion = string.IsNullOrWhiteSpace(version) ? null : version.Trim()
                };

                var json = JsonSerializer.Serialize(settings, SerializerOptions);
                File.WriteAllText(_settingsPath, json);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[AUTOUPDATE] Failed to write UI update user settings.");
            }
        }

        private sealed class UiUpdateUserSettings
        {
            public string? SkippedVersion { get; set; }
        }
    }
}
