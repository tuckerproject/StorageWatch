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
using StorageWatch.Services.Logging;

namespace StorageWatch.Services.Alerting
{
    /// <summary>
    /// Sends alert messages using SMTP email.
    /// </summary>
    public class SmtpAlertSender : IAlertSender
    {
        private readonly SmtpOptions _options;
        private readonly RollingFileLogger _logger;

        public SmtpAlertSender(SmtpOptions options, RollingFileLogger logger)
        {
            _options = options;
            _logger = logger;
        }

        public async Task SendAlertAsync(string message)
        {
            if (!_options.Enabled)
            {
                _logger.Log("[SMTP] Skipping send: SMTP is disabled in config.");
                return;
            }

            try
            {
                using var client = new SmtpClient(_options.Host, _options.Port)
                {
                    EnableSsl = _options.UseSsl,
                    Credentials = new NetworkCredential(_options.Username, _options.Password)
                };

                var mail = new MailMessage(_options.FromAddress, _options.ToAddress)
                {
                    Subject = "Disk Space Alert",
                    Body = message
                };

                _logger.Log("[SMTP] Sending alert email.");
                await client.SendMailAsync(mail);
            }
            catch (Exception ex)
            {
                _logger.Log($"[SMTP ERROR] Exception while sending alert: {ex}");
            }
        }
    }
}