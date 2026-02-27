/// <summary>
/// Alert Sender Plugin Manager
/// 
/// Manages the lifecycle of alert sender plugins, including:
/// - Plugin discovery and registration
/// - Plugin filtering based on configuration
/// - Plugin health monitoring
/// - DI-based plugin resolution
/// </summary>

using Microsoft.Extensions.DependencyInjection;
using StorageWatch.Config.Options;
using StorageWatch.Models;
using StorageWatch.Models.Plugins;
using StorageWatch.Services.Logging;
using System;
using System.Collections.Generic;
using System.Linq;

namespace StorageWatch.Services.Alerting.Plugins
{
    /// <summary>
    /// Manages alert sender plugins and provides instances based on configuration.
    /// </summary>
    public class AlertSenderPluginManager
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly StorageWatchOptions _options;
        private readonly RollingFileLogger _logger;
        private readonly AlertSenderPluginRegistry _registry;

        /// <summary>
        /// Initializes a new instance of the AlertSenderPluginManager class.
        /// </summary>
        /// <param name="serviceProvider">Service provider for resolving plugin instances.</param>
        /// <param name="options">Application configuration options.</param>
        /// <param name="logger">Logger instance.</param>
        /// <param name="registry">Plugin registry.</param>
        public AlertSenderPluginManager(
            IServiceProvider serviceProvider,
            StorageWatchOptions options,
            RollingFileLogger logger,
            AlertSenderPluginRegistry registry)
        {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        }

        /// <summary>
        /// Gets all enabled alert sender instances based on configuration.
        /// </summary>
        /// <returns>List of enabled alert sender instances.</returns>
        public List<IAlertSender> GetEnabledSenders()
        {
            var senders = new List<IAlertSender>();

            // Get all registered IAlertSender implementations from DI
            var allSenders = _serviceProvider.GetServices<IAlertSender>().ToList();

            _logger.Log($"[PLUGIN MANAGER] Found {allSenders.Count} registered alert sender plugin(s).");

            foreach (var sender in allSenders)
            {
                try
                {
                    // Check if this plugin is enabled based on legacy configuration
                    if (IsPluginEnabled(sender))
                    {
                        _logger.Log($"[PLUGIN MANAGER] Enabling plugin: {sender.Name}");
                        senders.Add(sender);
                    }
                    else
                    {
                        _logger.Log($"[PLUGIN MANAGER] Plugin disabled: {sender.Name}");
                    }
                }
                catch (Exception ex)
                {
                    _logger.Log($"[PLUGIN MANAGER ERROR] Failed to check plugin {sender.Name}: {ex.Message}");
                }
            }

            if (senders.Count == 0)
            {
                _logger.Log("[PLUGIN MANAGER] No alert sender plugins are enabled.");
            }

            return senders;
        }

        /// <summary>
        /// Checks if a specific plugin is enabled based on configuration.
        /// Supports both legacy (direct SMTP/GroupMe options) and new plugin configuration.
        /// </summary>
        /// <param name="sender">The alert sender to check.</param>
        /// <returns>True if the plugin is enabled, false otherwise.</returns>
        private bool IsPluginEnabled(IAlertSender sender)
        {
            // First check global notifications flag
            if (!_options.Alerting.EnableNotifications)
            {
                return false;
            }

            // Check new plugin configuration format
            if (_options.Alerting.Plugins.TryGetValue(sender.Name, out var pluginConfig))
            {
                if (pluginConfig.TryGetValue("Enabled", out var enabledValue))
                {
                    return Convert.ToBoolean(enabledValue);
                }
            }

            // Fall back to legacy configuration for backward compatibility
            switch (sender.Name)
            {
                case "SMTP":
                    return _options.Alerting.Smtp?.Enabled ?? false;
                case "GroupMe":
                    return _options.Alerting.GroupMe?.Enabled ?? false;
                default:
                    return false;
            }
        }

        /// <summary>
        /// Performs health checks on all enabled plugins.
        /// </summary>
        /// <returns>Dictionary mapping plugin names to their health status.</returns>
        public async Task<Dictionary<string, bool>> PerformHealthChecksAsync()
        {
            var results = new Dictionary<string, bool>();
            var senders = GetEnabledSenders();

            foreach (var sender in senders)
            {
                try
                {
                    _logger.Log($"[PLUGIN MANAGER] Running health check for: {sender.Name}");
                    var healthy = await sender.HealthCheckAsync();
                    results[sender.Name] = healthy;

                    if (!healthy)
                    {
                        _logger.Log($"[PLUGIN MANAGER] Health check FAILED for: {sender.Name}");
                    }
                }
                catch (Exception ex)
                {
                    _logger.Log($"[PLUGIN MANAGER ERROR] Health check exception for {sender.Name}: {ex.Message}");
                    results[sender.Name] = false;
                }
            }

            return results;
        }

        /// <summary>
        /// Gets information about all registered plugins.
        /// </summary>
        /// <returns>List of plugin metadata.</returns>
        public List<AlertSenderPluginMetadata> GetPluginInfo()
        {
            return _registry.Plugins.Values.ToList();
        }
    }
}
