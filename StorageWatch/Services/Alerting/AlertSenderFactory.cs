/// <summary>
/// Factory for Creating Alert Sender Instances
/// 
/// This factory class is responsible for instantiating the appropriate alert sender implementations
/// based on configuration settings. It supports multiple alert delivery methods (GroupMe, SMTP, etc.)
/// and can be easily extended with new alert backends by adding new factory logic and sender implementations.
/// </summary>

using StorageWatch.Config.Options;
using StorageWatch.Models;
using StorageWatch.Services.Logging;
using System.Collections.Generic;

namespace StorageWatch.Services.Alerting
{
    /// <summary>
    /// Static factory for building alert sender instances.
    /// </summary>
    public static class AlertSenderFactory
    {
        /// <summary>
        /// Builds a list of alert senders based on the application configuration.
        /// Each enabled alert delivery method (GroupMe, SMTP, etc.) will be instantiated
        /// and added to the returned list.
        /// </summary>
        /// <param name="options">The strongly-typed options containing alert delivery settings.</param>
        /// <param name="logger">The logger for recording factory operations.</param>
        /// <returns>A list of IAlertSender implementations corresponding to enabled delivery methods.
        /// Returns an empty list if no alert delivery methods are enabled.</returns>
        public static List<IAlertSender> BuildSenders(
            StorageWatchOptions options,
            RollingFileLogger logger)
        {
            var list = new List<IAlertSender>();

            // Check if GroupMe alerts are enabled and add a GroupMe sender if so
            if (options.Alerting?.GroupMe?.Enabled == true)
            {
                logger.Log("[ALERT FACTORY] Adding GroupMeAlertSender.");
                list.Add(new GroupMeAlertSender(options.Alerting.GroupMe, logger));
            }

            // Check if SMTP alerts are enabled and add an SMTP sender if so
            if (options.Alerting?.Smtp?.Enabled == true)
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
    }
}