using Microsoft.Extensions.Logging;
using StorageWatch.Shared.Update.Models;
using StorageWatchUI.Communication;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Manifest = StorageWatch.Shared.Update.Models.UpdateManifest;

namespace StorageWatchUI.Services.AutoUpdate
{
    public sealed class UpdateProgressInfo
    {
        public string StatusText { get; init; } = string.Empty;
        public double Progress { get; init; }
        public bool IsIndeterminate { get; init; }
    }

    public sealed class UnifiedUpdateResult
    {
        public bool UiUpdated { get; set; }
        public bool AgentUpdated { get; set; }
        public bool ServerUpdated { get; set; }
        public List<string> Errors { get; } = new();
    }

    public class UnifiedUpdateCoordinator
    {
        private readonly IUiAutoUpdateWorker _uiAutoUpdateWorker;
        private readonly ServiceCommunicationClient _serviceCommunicationClient;
        private readonly ILogger<UnifiedUpdateCoordinator> _logger;

        public UnifiedUpdateCoordinator(
            IUiAutoUpdateWorker uiAutoUpdateWorker,
            ServiceCommunicationClient serviceCommunicationClient,
            ILogger<UnifiedUpdateCoordinator> logger)
        {
            _uiAutoUpdateWorker = uiAutoUpdateWorker ?? throw new ArgumentNullException(nameof(uiAutoUpdateWorker));
            _serviceCommunicationClient = serviceCommunicationClient ?? throw new ArgumentNullException(nameof(serviceCommunicationClient));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<UnifiedUpdateResult> PerformUnifiedUpdateAsync(
            Manifest manifest,
            IProgress<UpdateProgressInfo> progress,
            CancellationToken cancellationToken)
        {
            if (manifest == null) throw new ArgumentNullException(nameof(manifest));
            if (progress == null) throw new ArgumentNullException(nameof(progress));

            var result = new UnifiedUpdateResult();

            progress.Report(new UpdateProgressInfo
            {
                StatusText = "Checking components...",
                Progress = 0,
                IsIndeterminate = true
            });

            var currentUiVersion = GetCurrentUiVersion();
            var currentAgentVersion = GetCurrentAgentVersion();
            var currentServerVersion = GetCurrentServerVersion();

            var uiNeedsUpdate = TryParseVersion(manifest.Ui.Version, out var manifestUiVersion) && manifestUiVersion > currentUiVersion;
            var agentNeedsUpdate = TryParseVersion(manifest.Agent.Version, out var manifestAgentVersion) && manifestAgentVersion > currentAgentVersion;
            var serverNeedsUpdate = TryParseVersion(manifest.Server.Version, out var manifestServerVersion) && manifestServerVersion > currentServerVersion;

            progress.Report(new UpdateProgressInfo
            {
                StatusText = "Checking components...",
                Progress = 10,
                IsIndeterminate = false
            });

            if (uiNeedsUpdate)
            {
                progress.Report(new UpdateProgressInfo
                {
                    StatusText = "Updating UI...",
                    Progress = 25,
                    IsIndeterminate = true
                });

                try
                {
                    var uiInstalled = await _uiAutoUpdateWorker.TryInstallAvailableUpdateAsync(cancellationToken);
                    if (uiInstalled)
                    {
                        result.UiUpdated = true;
                    }
                    else
                    {
                        result.Errors.Add("UI update did not complete successfully.");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Unified update failed during UI update phase.");
                    result.Errors.Add($"UI update failed: {ex.Message}");
                }
            }

            progress.Report(new UpdateProgressInfo
            {
                StatusText = "Updating UI...",
                Progress = 40,
                IsIndeterminate = false
            });

            if (agentNeedsUpdate)
            {
                progress.Report(new UpdateProgressInfo
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
                        result.Errors.Add($"Agent install request failed: {installResponse.ErrorMessage}");
                    }
                    else
                    {
                        progress.Report(new UpdateProgressInfo
                        {
                            StatusText = "Restarting Agent...",
                            Progress = 65,
                            IsIndeterminate = true
                        });

                        var restartResponse = await _serviceCommunicationClient.SendRestartServiceRequestAsync(cancellationToken);
                        if (!restartResponse.Success)
                        {
                            result.Errors.Add($"Agent restart request failed: {restartResponse.ErrorMessage}");
                        }
                        else
                        {
                            result.AgentUpdated = true;
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Unified update failed during Agent update phase.");
                    result.Errors.Add($"Agent update failed: {ex.Message}");
                }
            }

            progress.Report(new UpdateProgressInfo
            {
                StatusText = "Updating Agent...",
                Progress = 70,
                IsIndeterminate = false
            });

            if (serverNeedsUpdate)
            {
                progress.Report(new UpdateProgressInfo
                {
                    StatusText = "Updating Server...",
                    Progress = 85,
                    IsIndeterminate = true
                });

                try
                {
                    using var serverClient = new HttpClient();
                    var response = await serverClient.PostAsync("http://localhost:5001/api/update/install", content: null, cancellationToken);
                    var json = await response.Content.ReadAsStringAsync(cancellationToken);

                    if (response.IsSuccessStatusCode)
                    {
                        using var document = JsonDocument.Parse(json);
                        var root = document.RootElement;
                        var success = root.TryGetProperty("success", out var successProp) && successProp.GetBoolean();

                        if (success)
                        {
                            result.ServerUpdated = true;
                        }
                        else
                        {
                            var error = root.TryGetProperty("error", out var errorProp)
                                ? errorProp.GetString()
                                : "Server update failed.";
                            result.Errors.Add(error ?? "Server update failed.");
                        }
                    }
                    else
                    {
                        result.Errors.Add($"Server update request failed with status {(int)response.StatusCode}.");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Unified update failed during Server update phase.");
                    result.Errors.Add($"Server update failed: {ex.Message}");
                }
            }

            progress.Report(new UpdateProgressInfo
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

        private static Version GetCurrentUiVersion()
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

        private static Version GetCurrentAgentVersion()
        {
            return GetInstalledComponentVersion("StorageWatchAgent.exe");
        }

        private static Version GetCurrentServerVersion()
        {
            return GetInstalledComponentVersion("StorageWatchServer.exe");
        }

        private static Version GetInstalledComponentVersion(string executableName)
        {
            try
            {
                var baseDir = AppContext.BaseDirectory;
                var programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
                var candidates = new[]
                {
                    Path.Combine(baseDir, executableName),
                    Path.Combine(baseDir, "..", executableName),
                    Path.Combine(baseDir, "..", "..", executableName),
                    Path.Combine(programData, "StorageWatch", "Agent", executableName),
                    Path.Combine(programData, "StorageWatch", "Server", executableName),
                    Path.Combine(programData, "StorageWatch", "UI", executableName)
                };

                foreach (var candidate in candidates)
                {
                    var fullPath = Path.GetFullPath(candidate);
                    if (!System.IO.File.Exists(fullPath))
                        continue;

                    var fileVersion = FileVersionInfo.GetVersionInfo(fullPath).FileVersion;
                    if (!string.IsNullOrWhiteSpace(fileVersion) && Version.TryParse(fileVersion, out var version))
                        return version;
                }
            }
            catch
            {
            }

            return new Version(0, 0, 0, 0);
        }
    }
}
