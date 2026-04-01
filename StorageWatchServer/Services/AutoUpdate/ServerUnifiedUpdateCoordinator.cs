using Microsoft.Extensions.Logging;
using StorageWatch.Shared.Update.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Manifest = StorageWatch.Shared.Update.Models.UpdateManifest;

namespace StorageWatchServer.Services.AutoUpdate
{
    public class ServerUpdateProgressInfo
    {
        public string StatusText { get; set; } = string.Empty;
        public double Progress { get; set; }
        public bool IsIndeterminate { get; set; }
    }

    public class ServerUnifiedUpdateResult
    {
        public bool ServerUpdated { get; set; }
        public bool AgentUpdated { get; set; }
        public bool UiUpdated { get; set; }
        public List<string> Errors { get; } = new();
    }

    public class ServerUnifiedUpdateCoordinator
    {
        private readonly ServerAutoUpdateWorker _serverAutoUpdateWorker;
        private readonly ServiceCommunicationClient _serviceCommunicationClient;
        private readonly ILogger<ServerUnifiedUpdateCoordinator> _logger;

        public ServerUnifiedUpdateCoordinator(
            ServerAutoUpdateWorker serverAutoUpdateWorker,
            ServiceCommunicationClient serviceCommunicationClient,
            ILogger<ServerUnifiedUpdateCoordinator> logger)
        {
            _serverAutoUpdateWorker = serverAutoUpdateWorker ?? throw new ArgumentNullException(nameof(serverAutoUpdateWorker));
            _serviceCommunicationClient = serviceCommunicationClient ?? throw new ArgumentNullException(nameof(serviceCommunicationClient));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<ServerUnifiedUpdateResult> PerformUnifiedUpdateAsync(
            Manifest manifest,
            IProgress<ServerUpdateProgressInfo> progress,
            CancellationToken cancellationToken)
        {
            if (manifest == null) throw new ArgumentNullException(nameof(manifest));
            if (progress == null) throw new ArgumentNullException(nameof(progress));

            var result = new ServerUnifiedUpdateResult();

            progress.Report(new ServerUpdateProgressInfo
            {
                StatusText = "Checking components...",
                Progress = 0,
                IsIndeterminate = true
            });

            var currentServerVersion = GetCurrentServerVersion();
            var currentAgentVersion = new Version(0, 0, 0, 0);
            var currentUiVersion = new Version(0, 0, 0, 0);

            var serverNeedsUpdate = TryParseVersion(manifest.Server.Version, out var manifestServerVersion)
                && manifestServerVersion > currentServerVersion;
            var agentNeedsUpdate = TryParseVersion(manifest.Agent.Version, out var manifestAgentVersion)
                && manifestAgentVersion > currentAgentVersion;
            var uiNeedsUpdate = TryParseVersion(manifest.Ui.Version, out var manifestUiVersion)
                && manifestUiVersion > currentUiVersion;

            progress.Report(new ServerUpdateProgressInfo
            {
                StatusText = "Checking components...",
                Progress = 10,
                IsIndeterminate = false
            });

            if (uiNeedsUpdate)
            {
                progress.Report(new ServerUpdateProgressInfo
                {
                    StatusText = "Updating UI...",
                    Progress = 25,
                    IsIndeterminate = true
                });

                // TODO: Implement UI update via HTTP in Phase 5
                result.UiUpdated = false;
            }

            progress.Report(new ServerUpdateProgressInfo
            {
                StatusText = "Updating UI...",
                Progress = 40,
                IsIndeterminate = false
            });

            if (agentNeedsUpdate)
            {
                progress.Report(new ServerUpdateProgressInfo
                {
                    StatusText = "Updating Agent...",
                    Progress = 55,
                    IsIndeterminate = true
                });

                try
                {
                    var installResponse = await _serviceCommunicationClient.SendInstallUpdateRequestAsync(cancellationToken);
                    if (!installResponse.Success)
                    {
                        result.Errors.Add($"Agent install request failed: {installResponse.ErrorMessage ?? "Unknown error"}");
                    }
                    else
                    {
                        progress.Report(new ServerUpdateProgressInfo
                        {
                            StatusText = "Restarting Agent...",
                            Progress = 65,
                            IsIndeterminate = true
                        });

                        var restartResponse = await _serviceCommunicationClient.SendRestartServiceRequestAsync(cancellationToken);
                        if (!restartResponse.Success)
                        {
                            result.Errors.Add($"Agent restart request failed: {restartResponse.ErrorMessage ?? "Unknown error"}");
                        }
                        else
                        {
                            result.AgentUpdated = true;
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[AUTOUPDATE] Unified coordinator agent update phase failed.");
                    result.Errors.Add($"Agent update exception: {ex.Message}");
                }
            }

            progress.Report(new ServerUpdateProgressInfo
            {
                StatusText = "Updating Agent...",
                Progress = 70,
                IsIndeterminate = false
            });

            if (serverNeedsUpdate)
            {
                progress.Report(new ServerUpdateProgressInfo
                {
                    StatusText = "Updating Server...",
                    Progress = 85,
                    IsIndeterminate = true
                });

                try
                {
                    _serverAutoUpdateWorker.RequestManualInstall();
                    var installResult = await _serverAutoUpdateWorker.RunServerUpdateAsync(cancellationToken);
                    if (installResult.Success)
                    {
                        result.ServerUpdated = true;
                    }
                    else
                    {
                        result.Errors.Add($"Server update failed: {installResult.ErrorMessage ?? "Unknown error"}");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[AUTOUPDATE] Unified coordinator server update phase failed.");
                    result.Errors.Add($"Server update exception: {ex.Message}");
                }
            }

            progress.Report(new ServerUpdateProgressInfo
            {
                StatusText = "Finalizing update...",
                Progress = 100,
                IsIndeterminate = false
            });

            return result;
        }

        private static bool TryParseVersion(string value, out Version version)
        {
            version = new Version(0, 0, 0, 0);
            return !string.IsNullOrWhiteSpace(value) && Version.TryParse(value, out version);
        }

        private static Version GetCurrentServerVersion()
        {
            try
            {
                var location = Assembly.GetExecutingAssembly().Location;
                var fileVersion = FileVersionInfo.GetVersionInfo(location).FileVersion;
                return Version.Parse(fileVersion ?? "0.0.0.0");
            }
            catch
            {
                return new Version(0, 0, 0, 0);
            }
        }
    }
}
