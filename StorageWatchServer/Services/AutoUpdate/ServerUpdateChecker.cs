using StorageWatchServer.Config;
using StorageWatchServer.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace StorageWatchServer.Services.AutoUpdate
{
    public interface IServerUpdateChecker
    {
        Task<ComponentUpdateCheckResult> CheckForUpdateAsync(CancellationToken cancellationToken);
    }

    public class ServerUpdateChecker : IServerUpdateChecker
    {
        private static readonly JsonSerializerOptions SerializerOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        private readonly HttpClient _httpClient;
        private readonly IOptions<AutoUpdateOptions> _options;
        private readonly ILogger<ServerUpdateChecker> _logger;
        private readonly Version _currentVersion;

        public ServerUpdateChecker(
            HttpClient httpClient,
            ILogger<ServerUpdateChecker> logger,
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
                if (manifest?.Server == null)
                {
                    return new ComponentUpdateCheckResult
                    {
                        IsUpdateAvailable = false,
                        ErrorMessage = "Manifest could not be parsed or is missing server info."
                    };
                }

                Version manifestVersion;
                try
                {
                    manifestVersion = Version.Parse(manifest.Server.Version);
                }
                catch (Exception)
                {
                    return new ComponentUpdateCheckResult
                    {
                        IsUpdateAvailable = false,
                        ErrorMessage = "Manifest server version is invalid."
                    };
                }

                var updateAvailable = IsUpdateAvailable(_currentVersion, manifestVersion);
                return new ComponentUpdateCheckResult
                {
                    IsUpdateAvailable = updateAvailable,
                    Component = updateAvailable ? manifest.Server : null
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Manifest check failed");
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
            return manifestVersion.CompareTo(currentVersion) > 0;
        }

        private static Version GetAssemblyVersion()
        {
            var assembly = typeof(ServerUpdateChecker).Assembly;
            return assembly.GetName().Version ?? new Version(0, 0, 0, 0);
        }
    }
}
