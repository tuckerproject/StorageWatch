/// <summary>
/// Base Alert Sender Implementation
/// 
/// Provides common functionality for alert sender plugins, including:
/// - Message formatting from DiskStatus
/// - Common error handling patterns
/// - Logging infrastructure
/// </summary>

using StorageWatch.Models;
using StorageWatch.Services.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace StorageWatch.Services.Alerting
{
    /// <summary>
    /// Abstract base class for alert sender implementations.
    /// Provides common message formatting and error handling.
    /// </summary>
    public abstract class AlertSenderBase : IAlertSender
    {
        protected readonly RollingFileLogger Logger;
        private readonly string _machineName = Environment.MachineName;

        /// <summary>
        /// Initializes a new instance of the AlertSenderBase class.
        /// </summary>
        /// <param name="logger">The logger instance.</param>
        protected AlertSenderBase(RollingFileLogger logger)
        {
            Logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <inheritdoc/>
        public abstract string Name { get; }

        /// <inheritdoc/>
        public async Task SendAlertAsync(DiskStatus status, CancellationToken cancellationToken = default)
        {
            if (!IsEnabled())
            {
                Logger.Log($"[{Name}] Skipping send: {Name} is disabled in config.");
                return;
            }

            try
            {
                var message = FormatMessage(status);
                Logger.Log($"[{Name}] Sending alert.");
                await SendMessageAsync(message, cancellationToken);
            }
            catch (Exception ex)
            {
                Logger.Log($"[{Name} ERROR] Exception while sending alert: {ex}");
            }
        }

        /// <inheritdoc/>
        public virtual Task<bool> HealthCheckAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(IsEnabled());
        }

        /// <summary>
        /// Formats a DiskStatus into an alert message.
        /// Override this method to customize message formatting per plugin.
        /// </summary>
        /// <param name="status">The disk status to format.</param>
        /// <returns>A formatted alert message.</returns>
        protected virtual string FormatMessage(DiskStatus status)
        {
            if (status.TotalSpaceGb == 0)
            {
                return $"ALERT — {_machineName}: Drive {status.DriveName} is NOT READY or unavailable.";
            }

            if (status.PercentFree < 10) // This threshold should come from monitoring options, but using a default for message formatting
            {
                return $"ALERT — {_machineName}: Drive {status.DriveName} is below threshold. " +
                       $"{status.FreeSpaceGb:F2} GB free ({status.PercentFree:F2}%).";
            }
            else
            {
                return $"RECOVERY — {_machineName}: Drive {status.DriveName} has recovered. " +
                       $"{status.FreeSpaceGb:F2} GB free ({status.PercentFree:F2}%).";
            }
        }

        /// <summary>
        /// Checks if this alert sender is enabled in configuration.
        /// Must be implemented by derived classes.
        /// </summary>
        /// <returns>True if enabled, false otherwise.</returns>
        protected abstract bool IsEnabled();

        /// <summary>
        /// Sends the formatted message using the specific delivery method.
        /// Must be implemented by derived classes.
        /// </summary>
        /// <param name="message">The formatted message to send.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        protected abstract Task SendMessageAsync(string message, CancellationToken cancellationToken);
    }
}
