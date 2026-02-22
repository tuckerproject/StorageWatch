using StorageWatch.Config.Options;
using StorageWatch.Models;
using StorageWatch.Services.Alerting.Plugins;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace StorageWatch.Services.AutoUpdate
{
    public interface IPluginUpdateChecker
    {
        Task<PluginUpdateCheckResult> CheckForUpdatesAsync(CancellationToken cancellationToken);
    }

    public class PluginUpdateChecker : IPluginUpdateChecker
    {
        private readonly HttpClient _httpClient;
        private readonly IOptions<AutoUpdateOptions> _options;
        private readonly ILogger<PluginUpdateChecker> _logger;

        public PluginUpdateChecker(
            HttpClient httpClient,
            ILogger<PluginUpdateChecker> logger,
            IOptions<AutoUpdateOptions> options)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _options = options ?? throw new ArgumentNullException(nameof(options));
        }

        public async Task<PluginUpdateCheckResult> CheckForUpdatesAsync(CancellationToken cancellationToken)
        {
            var options = _options.Value;
            if (string.IsNullOrWhiteSpace(options.ManifestUrl))
            {
                return new PluginUpdateCheckResult
                {
                    Updates = Array.Empty<PluginUpdateInfo>(),
                    ErrorMessage = "ManifestUrl is not configured."
                };
            }

            var registry = AlertSenderPluginRegistry.Current;
            
            try
            {
                var response = await _httpClient.GetAsync(options.ManifestUrl, cancellationToken);
                if (!response.IsSuccessStatusCode)
                {
                    return new PluginUpdateCheckResult
                    {
                        Updates = Array.Empty<PluginUpdateInfo>(),
                        ErrorMessage = $"Manifest request failed with status {(int)response.StatusCode}."
                    };
                }

                var json = await response.Content.ReadAsStringAsync(cancellationToken);
                var manifest = ServiceUpdateChecker.ParseManifest(json);
                if (manifest?.Plugins == null || manifest.Plugins.Count == 0)
                {
                    return new PluginUpdateCheckResult { Updates = Array.Empty<PluginUpdateInfo>() };
                }

                var updates = new List<PluginUpdateInfo>();
                foreach (var plugin in manifest.Plugins)
                {
                    if (string.IsNullOrWhiteSpace(plugin.Name))
                        continue;

                    var metadata = registry?.GetPlugin(plugin.Name);
                    if (metadata == null)
                    {
                        updates.Add(plugin);
                        continue;
                    }

                    if (!Version.TryParse(metadata.Version, out var currentVersion))
                    {
                        _logger.LogWarning("[AUTOUPDATE] Plugin '{PluginName}' has invalid local version '{Version}'.", plugin.Name, metadata.Version);
                        continue;
                    }

                    if (!Version.TryParse(plugin.Version, out var manifestVersion))
                    {
                        _logger.LogWarning("[AUTOUPDATE] Plugin '{PluginName}' has invalid manifest version '{Version}'.", plugin.Name, plugin.Version);
                        continue;
                    }

                    if (manifestVersion > currentVersion)
                    {
                        updates.Add(plugin);
                    }
                }

                return new PluginUpdateCheckResult { Updates = updates };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[AUTOUPDATE] Plugin manifest check failed");
                return new PluginUpdateCheckResult
                {
                    Updates = Array.Empty<PluginUpdateInfo>(),
                    ErrorMessage = ex.Message
                };
            }
        }
    }
}
