/// <summary>
/// Factory for Creating Alert Sender Instances
/// 
/// This factory class is responsible for instantiating the appropriate alert sender implementations
/// based on configuration settings. It supports multiple alert delivery methods (GroupMe, SMTP, etc.)
/// and uses a plugin architecture for extensibility.
/// 
/// The factory now delegates to the AlertSenderPluginManager for plugin discovery and filtering.
/// This maintains backward compatibility while enabling future extensibility through plugins.
/// </summary>

using StorageWatch.Config.Options;
using StorageWatch.Models;
using StorageWatch.Services.Alerting.Plugins;
using StorageWatch.Services.Logging;
using System;
using System.Collections.Generic;

namespace StorageWatch.Services.Alerting
{
    /// <summary>
    /// Factory for building alert sender instances using the plugin architecture.
    /// </summary>
    public static class AlertSenderFactory
    {
        /// <summary>
        /// Builds a list of alert senders based on the application configuration.
        /// Each enabled alert delivery method (GroupMe, SMTP, etc.) will be instantiated
        /// and added to the returned list.
        /// 
        /// LEGACY METHOD: This method is maintained for backward compatibility.
        /// New code should use AlertSenderPluginManager directly via dependency injection.
        /// </summary>
        /// <param name="options">The strongly-typed options containing alert delivery settings.</param>
        /// <param name="logger">The logger for recording factory operations.</param>
        /// <returns>A list of IAlertSender implementations corresponding to enabled delivery methods.
        /// Returns an empty list if no alert delivery methods are enabled.</returns>
        [Obsolete("Use AlertSenderPluginManager via dependency injection instead. This method is maintained for backward compatibility.")]
        public static List<IAlertSender> BuildSenders(
            StorageWatchOptions options,
            RollingFileLogger logger)
        {
            var list = new List<IAlertSender>();

            if (options?.Alerting == null)
            {
                logger.Log("[ALERT FACTORY] Alerting options are null.");
                return list;
            }

            // Check if GroupMe alerts are enabled and add a GroupMe sender if so
            if (options.Alerting.GroupMe?.Enabled == true)
            {
                logger.Log("[ALERT FACTORY] Adding GroupMeAlertSender.");
                list.Add(new GroupMeAlertSender(options.Alerting.GroupMe, logger));
            }

            // Check if SMTP alerts are enabled and add an SMTP sender if so
            if (options.Alerting.Smtp?.Enabled == true)
            {
                logger.Log("[ALERT FACTORY] Adding SmtpAlertSender.");
                list.Add(new SmtpAlertSender(options.Alerting.Smtp, logger));
            }

            // Log a warning if no alert senders are enabled (alerts will be silently dropped)
            if (list.Count == 0)
            {
                logger.Log("[ALERT FACTORY] No alert senders enabled in config.");
            }

            return list;
        }

        /// <summary>
        /// Creates an AlertSenderPluginManager instance for managing plugins.
        /// This is the recommended approach for new code.
        /// </summary>
        /// <param name="serviceProvider">Service provider for resolving dependencies.</param>
        /// <param name="options">Application configuration options.</param>
        /// <param name="logger">Logger instance.</param>
        /// <param name="registry">Plugin registry. If null, creates a new one with auto-discovery.</param>
        /// <returns>Configured AlertSenderPluginManager instance.</returns>
        public static AlertSenderPluginManager CreatePluginManager(
            IServiceProvider serviceProvider,
            StorageWatchOptions options,
            RollingFileLogger logger,
            AlertSenderPluginRegistry? registry = null)
        {
            if (registry == null)
            {
                registry = new AlertSenderPluginRegistry();
                registry.DiscoverPlugins(); // Auto-discover plugins in current assembly
            }

            return new AlertSenderPluginManager(serviceProvider, options, logger, registry);
        }
    }
}