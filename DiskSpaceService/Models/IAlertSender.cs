/// <summary>
/// Alert Sender Interface
/// 
/// Defines the contract for alert delivery mechanisms. Implementations include
/// GroupMe, SMTP email, and any future alerting backends. This interface allows
/// for flexible, pluggable alert delivery without tightly coupling the monitoring
/// logic to specific alert delivery methods.
/// </summary>

namespace DiskSpaceService.Models
{
    /// <summary>
    /// Defines a common interface for all alert delivery mechanisms.
    /// Implementations include GroupMe, SMTP, and any future alerting backends.
    /// </summary>
    public interface IAlertSender
    {
        /// <summary>
        /// Sends an alert message using the configured delivery method.
        /// </summary>
        Task SendAlertAsync(string message);
    }
}