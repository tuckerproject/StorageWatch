/// <summary>
/// Alert Sender Interface
/// 
/// Defines the contract for alert delivery mechanisms. Implementations include
/// GroupMe, SMTP email, and any future alerting backends. This interface allows
/// for flexible, pluggable alert delivery without tightly coupling the monitoring
/// logic to specific alert delivery methods.
/// 
/// This interface supports a plugin architecture where alert senders can be:
/// - Built-in (SMTP, GroupMe)
/// - External (loaded from assemblies)
/// - Dynamically enabled/disabled via configuration
/// </summary>

namespace StorageWatch.Models
{
    /// <summary>
    /// Defines a common interface for all alert delivery mechanisms.
    /// Implementations include GroupMe, SMTP, and any future alerting backends.
    /// </summary>
    public interface IAlertSender
    {
        /// <summary>
        /// Gets the unique name of this alert sender plugin.
        /// Used for identification, logging, and configuration mapping.
        /// Examples: "SMTP", "GroupMe", "Slack", "Teams"
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Sends an alert message using the configured delivery method.
        /// Implementations should handle errors gracefully and not throw exceptions.
        /// </summary>
        /// <param name="status">The disk status that triggered the alert.</param>
        /// <param name="cancellationToken">Cancellation token to support graceful shutdown.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task SendAlertAsync(DiskStatus status, CancellationToken cancellationToken = default);

        /// <summary>
        /// Optional: Checks if this alert sender is healthy and can send alerts.
        /// Implementations can verify connectivity, credentials, configuration, etc.
        /// Returns true if the sender is ready, false otherwise.
        /// Default implementation returns true (assume healthy).
        /// </summary>
        /// <param name="cancellationToken">Cancellation token for the health check.</param>
        /// <returns>True if healthy and ready to send alerts, false otherwise.</returns>
        Task<bool> HealthCheckAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(true);
        }
    }
}