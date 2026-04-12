using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using StorageWatchServer.Services.AutoUpdate;
using System;
using System.Diagnostics;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace StorageWatchServer.Controllers
{
    /// <summary>
    /// API endpoints for update status and server updater handoff operations.
    /// </summary>
    [Route("api/update")]
    [ApiController]
    public class UpdateController : ControllerBase
    {
        private readonly IServerUpdateChecker _updateChecker;
        private readonly IManifestProvider _manifestProvider;
        private readonly ServerAutoUpdateWorker _autoUpdateWorker;
        private readonly IServerRestartHandler _restartHandler;
        private readonly ILogger<UpdateController> _logger;

        public UpdateController(
            IServerUpdateChecker updateChecker,
            IManifestProvider manifestProvider,
            ServerAutoUpdateWorker autoUpdateWorker,
            IServerRestartHandler restartHandler,
            ILogger<UpdateController> logger)
        {
            _updateChecker = updateChecker ?? throw new ArgumentNullException(nameof(updateChecker));
            _manifestProvider = manifestProvider ?? throw new ArgumentNullException(nameof(manifestProvider));
            _autoUpdateWorker = autoUpdateWorker ?? throw new ArgumentNullException(nameof(autoUpdateWorker));
            _restartHandler = restartHandler ?? throw new ArgumentNullException(nameof(restartHandler));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Returns current and latest versions for Server, Agent, and UI components.
        /// </summary>
        [HttpGet("status")]
        public async Task<ActionResult<ServerUpdateStatusDto>> GetStatus(CancellationToken cancellationToken)
        {
            var currentServerVersion = GetCurrentVersionString();
            var currentAgentVersion = GetInstalledComponentVersion("StorageWatchAgent.exe");
            var currentUiVersion = GetInstalledComponentVersion("StorageWatchUI.exe");

            var latestServerVersion = string.Empty;
            var latestAgentVersion = string.Empty;
            var latestUiVersion = string.Empty;

            var manifest = await _manifestProvider.GetManifestAsync(cancellationToken);
            if (manifest != null)
            {
                latestServerVersion = manifest.Server?.Version ?? string.Empty;
                latestAgentVersion = manifest.Agent?.Version ?? string.Empty;
                latestUiVersion = manifest.Ui?.Version ?? string.Empty;
            }

            var serverUpdateAvailable = IsUpdateAvailable(currentServerVersion, latestServerVersion);
            var agentUpdateAvailable = IsUpdateAvailable(currentAgentVersion, latestAgentVersion);
            var uiUpdateAvailable = IsUpdateAvailable(currentUiVersion, latestUiVersion);
            var updateAvailable = serverUpdateAvailable || agentUpdateAvailable || uiUpdateAvailable;

            if (string.IsNullOrWhiteSpace(latestServerVersion))
            {
                var checkResult = await _updateChecker.CheckForUpdateAsync(cancellationToken);
                if (checkResult.IsUpdateAvailable && checkResult.Component != null)
                {
                    latestServerVersion = checkResult.Component.Version;
                    updateAvailable = true;
                }
            }

            return Ok(new ServerUpdateStatusDto
            {
                CurrentVersion = currentServerVersion,
                CurrentServerVersion = currentServerVersion,
                CurrentAgentVersion = currentAgentVersion,
                CurrentUiVersion = currentUiVersion,
                LatestVersion = latestServerVersion,
                LatestServerVersion = latestServerVersion,
                LatestAgentVersion = latestAgentVersion,
                LatestUiVersion = latestUiVersion,
                UpdateAvailable = updateAvailable,
                IsInstalling = _autoUpdateWorker.IsInstalling
            });
        }

        /// <summary>
        /// Starts the server update flow (prepare, stage, handoff, exit).
        /// </summary>
        [HttpPost("install")]
        public async Task<ActionResult<UpdateInstallResponseDto>> Install(CancellationToken cancellationToken)
        {
            _logger.LogInformation("[API] Server update install requested");

            try
            {
                _autoUpdateWorker.RequestManualInstall();
                var installResult = await _autoUpdateWorker.RunServerUpdateAsync(cancellationToken);
                if (!installResult.Success)
                {
                    var error = installResult.ErrorMessage ?? "Server update install failed.";
                    _logger.LogError("[API] Server update install failed: {Error}", error);
                    return Ok(new UpdateInstallResponseDto
                    {
                        Success = false,
                        Error = error
                    });
                }

                _logger.LogInformation("[API] Server update install completed");
                return Ok(new UpdateInstallResponseDto
                {
                    Success = true,
                    Error = string.Empty
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[API] Server update install failed: {Error}", ex.Message);
                return Ok(new UpdateInstallResponseDto
                {
                    Success = false,
                    Error = ex.Message
                });
            }
        }

        /// <summary>
        /// Accepted for compatibility; restart behavior is delegated to updater handoff flows.
        /// </summary>
        [HttpPost("restart")]
        public ActionResult<UpdateInstallResponseDto> Restart()
        {
            _logger.LogInformation("[API] Server restart requested");

            try
            {
                _restartHandler.RequestRestart();
                _logger.LogInformation("[API] Server restart completed");
                return Ok(new UpdateInstallResponseDto
                {
                    Success = true,
                    Error = string.Empty
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[API] Server restart failed: {Error}", ex.Message);
                return Ok(new UpdateInstallResponseDto
                {
                    Success = false,
                    Error = ex.Message
                });
            }
        }

        private static string GetCurrentVersionString()
        {
            try
            {
                var location = Assembly.GetExecutingAssembly().Location;
                return FileVersionInfo.GetVersionInfo(location).FileVersion ?? "0.0.0.0";
            }
            catch
            {
                return "0.0.0.0";
            }
        }

        private static string GetInstalledComponentVersion(string executableName)
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

                    var version = FileVersionInfo.GetVersionInfo(fullPath).FileVersion;
                    if (!string.IsNullOrWhiteSpace(version))
                        return version;
                }
            }
            catch
            {
            }

            return "0.0.0.0";
        }

        private static bool IsUpdateAvailable(string currentVersion, string latestVersion)
        {
            return Version.TryParse(currentVersion, out var current)
                && Version.TryParse(latestVersion, out var latest)
                && latest > current;
        }
    }

    /// <summary>
    /// Version and install-state payload for update status requests.
    /// </summary>
    public class ServerUpdateStatusDto
    {
        public string CurrentVersion { get; set; } = string.Empty;
        public string CurrentServerVersion { get; set; } = string.Empty;
        public string CurrentAgentVersion { get; set; } = string.Empty;
        public string CurrentUiVersion { get; set; } = string.Empty;
        public string LatestVersion { get; set; } = string.Empty;
        public string LatestServerVersion { get; set; } = string.Empty;
        public string LatestAgentVersion { get; set; } = string.Empty;
        public string LatestUiVersion { get; set; } = string.Empty;
        public bool UpdateAvailable { get; set; }
        public bool IsInstalling { get; set; }
    }

    /// <summary>
    /// Response payload for update start requests.
    /// </summary>
    public class UpdateInstallResponseDto
    {
        public bool Success { get; set; }
        public string Error { get; set; } = string.Empty;
    }
}
