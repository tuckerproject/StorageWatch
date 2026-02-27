/// <summary>
/// SMTP Email Alert Sender
/// 
/// Implements alert delivery via SMTP email. Sends disk space alerts as email messages
/// to configured recipients using standard SMTP server settings. Supports SSL/TLS encryption
/// and authentication with username and password.
/// </summary>

using System.Net;
using System.Net.Mail;
using StorageWatch.Config.Options;
using StorageWatch.Models;
using StorageWatch.Models.Plugins;
using StorageWatch.Services.Logging;

namespace StorageWatch.Services.Alerting
{
    /// <summary>
    /// Sends alert messages using SMTP email.
    /// </summary>
    [AlertSenderPlugin("SMTP", Description = "Sends alerts via SMTP email", Version = "2.0.0")]
    public class SmtpAlertSender : AlertSenderBase
    {
        private readonly SmtpOptions _options;

        public SmtpAlertSender(SmtpOptions options, RollingFileLogger logger)
            : base(logger)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
        }

        /// <inheritdoc/>
        public override string Name => "SMTP";

        /// <inheritdoc/>
        protected override bool IsEnabled() => _options.Enabled;

        /// <inheritdoc/>
        protected override async Task SendMessageAsync(string message, CancellationToken cancellationToken)
        {
            using var client = new SmtpClient(_options.Host, _options.Port)
            {
                EnableSsl = _options.UseSsl,
                Credentials = new NetworkCredential(_options.Username, _options.Password)
            };

            var mail = new MailMessage(_options.FromAddress, _options.ToAddress)
            {
                Subject = "StorageWatch Disk Space Alert",
                Body = message
            };

            await client.SendMailAsync(mail, cancellationToken);
        }

        /// <inheritdoc/>
        public override async Task<bool> HealthCheckAsync(CancellationToken cancellationToken = default)
        {
            if (!IsEnabled())
                return false;

            // Basic configuration validation
            if (string.IsNullOrWhiteSpace(_options.Host) ||
                string.IsNullOrWhiteSpace(_options.FromAddress) ||
                string.IsNullOrWhiteSpace(_options.ToAddress))
            {
                Logger.Log("[SMTP] Health check failed: Missing required configuration.");
                return false;
            }

            return true;
        }
    }
}