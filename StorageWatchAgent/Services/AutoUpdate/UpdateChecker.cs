using System.Diagnostics;
using StorageWatch.Config.Options;
using StorageWatch.Shared.Update.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace StorageWatch.Services.AutoUpdate
{
    public interface IServiceUpdateChecker
    {
        Task<ComponentUpdateCheckResult> CheckForUpdateAsync(CancellationToken cancellationToken);
    }

    public class ServiceUpdateChecker : IServiceUpdateChecker
    {
        private static readonly JsonSerializerOptions SerializerOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        private readonly HttpClient _httpClient;
        private readonly IOptions<AutoUpdateOptions> _options;
        private readonly ILogger<ServiceUpdateChecker> _logger;
        private readonly Version _currentVersion;

        public ServiceUpdateChecker(
            HttpClient httpClient,
            ILogger<ServiceUpdateChecker> logger,
            IOptions<AutoUpdateOptions> options)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _currentVersion = GetAssemblyVersion();
        }

        public async Task<ComponentUpdateCheckResult> CheckForUpdateAsync(CancellationToken cancellationToken)
        {
            var options = _options.Value;
            if (string.IsNullOrWhiteSpace(options.ManifestUrl))
            {
                return new ComponentUpdateCheckResult
                {
                    IsUpdateAvailable = false,
                    ErrorMessage = "ManifestUrl is not configured."
                };
            }

            try
            {
                var response = await _httpClient.GetAsync(options.ManifestUrl, cancellationToken);
                if (!response.IsSuccessStatusCode)
                {
                    return new ComponentUpdateCheckResult
                    {
                        IsUpdateAvailable = false,
                        ErrorMessage = $"Manifest request failed with status {(int)response.StatusCode}."
                    };
                }

                var json = await response.Content.ReadAsStringAsync(cancellationToken);
                var manifest = ParseManifest(json);
                if (manifest == null)
                {
                    return new ComponentUpdateCheckResult
                    {
                        IsUpdateAvailable = false,
                        ErrorMessage = "Manifest could not be parsed."
                    };
                }

                var component = manifest.Agent;
                if (component == null)
                {
                    return new ComponentUpdateCheckResult
                    {
                        IsUpdateAvailable = false,
                        ErrorMessage = "Manifest could not be parsed or is missing agent info."
                    };
                }

                if (!Version.TryParse(component.Version, out var manifestVersion))
                {
                    return new ComponentUpdateCheckResult
                    {
                        IsUpdateAvailable = false,
                        ErrorMessage = "Manifest agent version is invalid."
                    };
                }

                var updateAvailable = IsUpdateAvailable(_currentVersion, manifestVersion);
                return new ComponentUpdateCheckResult
                {
                    IsUpdateAvailable = updateAvailable,
                    Component = updateAvailable ? component : null
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[AUTOUPDATE] Manifest check failed");
                return new ComponentUpdateCheckResult
                {
                    IsUpdateAvailable = false,
                    ErrorMessage = ex.Message
                };
            }
        }

        public static UpdateManifest? ParseManifest(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                return null;

            return JsonSerializer.Deserialize<UpdateManifest>(json, SerializerOptions);
        }

        public static bool IsUpdateAvailable(Version currentVersion, Version manifestVersion)
        {
            return manifestVersion > currentVersion;
        }

        private static Version GetAssemblyVersion()
        {
            try
            {
                var location = typeof(ServiceUpdateChecker).Assembly.Location;
                var fileVersion = FileVersionInfo.GetVersionInfo(location).FileVersion;
                if (!string.IsNullOrWhiteSpace(fileVersion) && Version.TryParse(fileVersion, out var version))
                    return version;
            }
            catch { }
            return new Version(0, 0, 0, 0);
        }
    }
}
