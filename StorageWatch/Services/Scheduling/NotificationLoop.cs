/// <summary>
/// Notification Loop for Disk Space Alerts
/// 
/// This class implements the main disk monitoring and alerting loop that runs continuously
/// in the background. It monitors each configured drive's free space against the configured
/// threshold and sends alerts when disk space status changes (e.g., from normal to low, or vice versa).
/// 
/// Features:
/// - Tracks disk space state per drive to avoid sending duplicate alerts
/// - Persists alert state to disk for recovery across service restarts
/// - Implements network readiness checks before sending alerts
/// - Supports multiple alert delivery methods (GroupMe, SMTP, etc.)
/// </summary>

using StorageWatch.Config.Options;
using StorageWatch.Models;
using StorageWatch.Services.Alerting;
using StorageWatch.Services.Logging;
using StorageWatch.Services.Monitoring;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace StorageWatch.Services.Scheduling
{
    /// <summary>
    /// Continuously monitors disk space and sends alerts based on state changes.
    /// </summary>
    public class NotificationLoop
    {
        private readonly StorageWatchOptions _options;
        private readonly List<IAlertSender> _senders;
        private readonly DiskAlertMonitor _monitor;
        private readonly RollingFileLogger _logger;

        // Dictionary to track the last known state for each drive (e.g., "ALERT", "NORMAL", "NOT_READY")
        // This prevents sending the same alert multiple times for the same condition
        private readonly Dictionary<string, string> _lastAlertState = new();
        private readonly string _machineName = Environment.MachineName;

        // Path to the JSON file that persists alert state between service restarts
        private readonly string _stateFilePath =
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                "StorageWatch",
                "alert_state.json"
            );

        /// <summary>
        /// Initializes a new instance of the NotificationLoop class.
        /// Loads the last known alert state from disk if it exists.
        /// </summary>
        /// <param name="options">Strongly-typed application options.</param>
        /// <param name="senders">List of alert senders for various delivery methods.</param>
        /// <param name="monitor">The disk monitor for checking drive status.</param>
        /// <param name="logger">Logger for recording operations.</param>
        public NotificationLoop(
            StorageWatchOptions options,
            List<IAlertSender> senders,
            DiskAlertMonitor monitor,
            RollingFileLogger logger)
        {
            _options = options;
            _senders = senders;
            _monitor = monitor;
            _logger = logger;

            // Ensure the state directory exists before attempting to load state
            Directory.CreateDirectory(Path.GetDirectoryName(_stateFilePath)!);
            LoadState();
        }

        /// <summary>
        /// Runs the main monitoring loop.
        /// Continuously checks each drive's free space and sends alerts on state transitions.
        /// </summary>
        /// <param name="token">Cancellation token for stopping the loop.</param>
        public async Task RunAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    // Check each configured drive
                    foreach (var driveLetter in _options.Monitoring.Drives)
                    {
                        // Get the current disk status for this drive
                        var status = _monitor.GetStatus(driveLetter);

                        string state;
                        string message;

                        // ====================================================================
                        // Determine the current state of the drive
                        // ====================================================================

                        // Case 1: Drive NOT READY - the drive is disconnected, unmounted, or otherwise unavailable
                        if (status.TotalSpaceGb == 0)
                        {
                            state = "NOT_READY";
                            message =
                                $"ALERT — {_machineName}: Drive {status.DriveName} is NOT READY or unavailable.";
                        }
                        else
                        {
                            // Case 2: Drive is READY - check if free space is below the threshold
                            bool belowThreshold = status.PercentFree < _options.Monitoring.ThresholdPercent;

                            if (belowThreshold)
                            {
                                // Drive space is critically low
                                state = "ALERT";
                                message =
                                    $"ALERT — {_machineName}: Drive {status.DriveName} is below threshold. " +
                                    $"{status.FreeSpaceGb:F2} GB free ({status.PercentFree:F2}%).";
                            }
                            else
                            {
                                // Drive space has recovered or is normal
                                state = "NORMAL";
                                message =
                                    $"RECOVERY — {_machineName}: Drive {status.DriveName} has recovered. " +
                                    $"{status.FreeSpaceGb:F2} GB free ({status.PercentFree:F2}%).";
                            }
                        }

                        // ====================================================================
                        // Compare current state with the last known state
                        // ====================================================================
                        _lastAlertState.TryGetValue(driveLetter, out var lastState);

                        if (lastState != state)
                        {
                            // State has changed - we may need to send an alert

                            // Check: If no senders are configured, skip network check and just update state
                            if (_senders.Count == 0)
                            {
                                _logger.Log("[ALERT FACTORY] No alert senders enabled in config.");
                                _lastAlertState[driveLetter] = state;
                                SaveState();
                                continue;
                            }

                            // Check: Only perform network readiness check if GroupMe is enabled
                            // (GroupMe requires internet connectivity, SMTP might not depending on network config)
                            if (GroupMeEnabled() && !NetworkReady())
                            {
                                _logger.Log("[ALERT] Network not ready — delaying alert send.");
                                // Don't update state yet; we'll retry on the next iteration
                                continue;
                            }

                            // ================================================================
                            // Send the alert through all configured senders
                            // ================================================================
                            foreach (var sender in _senders)
                                await sender.SendAlertAsync(status, token);

                            _logger.Log($"[ALERT] State change for {driveLetter}: {lastState} → {state}");

                            // Update and persist the new state
                            _lastAlertState[driveLetter] = state;
                            SaveState();
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.Log($"[ALERT] Notification loop error: {ex}");
                }

                // Wait 1 minute before checking again
                await Task.Delay(TimeSpan.FromMinutes(1), token);
            }
        }

        /// <summary>
        /// Detects if a GroupMe alert sender is configured and enabled.
        /// Used to determine if network connectivity checks are necessary.
        /// </summary>
        /// <returns>True if a GroupMe sender exists in the configured senders list.</returns>
        private bool GroupMeEnabled()
        {
            foreach (var sender in _senders)
            {
                if (sender.GetType().Name.Contains("GroupMe", StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Checks network readiness by attempting to resolve the GroupMe API hostname.
        /// This ensures that network connectivity is available before attempting to send alerts.
        /// </summary>
        /// <returns>True if the network is accessible (DNS resolution successful), false otherwise.</returns>
        private bool NetworkReady()
        {
            try
            {
                // Attempt to resolve the GroupMe API server's hostname
                // If successful, the network is available
                Dns.GetHostEntry("api.groupme.com");
                return true;
            }
            catch
            {
                // DNS resolution failed - network is not available or GroupMe is unreachable
                return false;
            }
        }

        /// <summary>
        /// Loads the persisted alert state from the JSON state file.
        /// This allows the service to maintain state across restarts.
        /// </summary>
        private void LoadState()
        {
            try
            {
                if (File.Exists(_stateFilePath))
                {
                    var json = File.ReadAllText(_stateFilePath);
                    var dict = JsonSerializer.Deserialize<Dictionary<string, string>>(json);

                    if (dict != null)
                    {
                        // Populate the in-memory state dictionary from the persisted file
                        foreach (var kv in dict)
                            _lastAlertState[kv.Key] = kv.Value;

                        _logger.Log("[STATE] Loaded alert state file.");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Log($"[STATE] Failed to load state file: {ex}");
            }
        }

        /// <summary>
        /// Persists the current alert state to the JSON state file.
        /// This ensures that state is retained even if the service is stopped and restarted.
        /// </summary>
        private void SaveState()
        {
            try
            {
                var json = JsonSerializer.Serialize(_lastAlertState);
                File.WriteAllText(_stateFilePath, json);
            }
            catch (Exception ex)
            {
                _logger.Log($"[STATE] Failed to save state file: {ex}");
            }
        }
    }
}