using Microsoft.Extensions.Options;
using StorageWatch.Shared.Update.Models;
using StorageWatchServer.Config;
using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace StorageWatchServer.Services.AutoUpdate
{
    public interface IManifestProvider
    {
        Task<UpdateManifest?> GetManifestAsync(CancellationToken cancellationToken);
    }

    public class ManifestProvider : IManifestProvider
    {
        private readonly HttpClient _httpClient;
        private readonly IOptions<AutoUpdateOptions> _options;

        public ManifestProvider(HttpClient httpClient, IOptions<AutoUpdateOptions> options)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _options = options ?? throw new ArgumentNullException(nameof(options));
        }

        public async Task<UpdateManifest?> GetManifestAsync(CancellationToken cancellationToken)
        {
            var options = _options.Value;
            if (string.IsNullOrWhiteSpace(options.ManifestUrl))
                return null;

            var response = await _httpClient.GetAsync(options.ManifestUrl, cancellationToken);
            if (!response.IsSuccessStatusCode)
                return null;

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            return ServerUpdateChecker.ParseManifest(json);
        }
    }
}
