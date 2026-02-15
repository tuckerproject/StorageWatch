/// <summary>
/// Validation for StorageWatch Options
/// 
/// Implements IValidateOptions for custom validation rules beyond DataAnnotations.
/// </summary>

using Microsoft.Extensions.Options;

namespace StorageWatch.Config.Options
{
    /// <summary>
    /// Validates root StorageWatchOptions configuration.
    /// Ensures all required sections are properly configured and consistent.
    /// </summary>
    public class StorageWatchOptionsValidator : IValidateOptions<StorageWatchOptions>
    {
        /// <summary>
        /// Validates the StorageWatchOptions.
        /// </summary>
        public ValidateOptionsResult Validate(string? name, StorageWatchOptions options)
        {
            if (options == null)
                return ValidateOptionsResult.Fail("StorageWatchOptions is null");

            // Ensure all sub-options are initialized
            if (options.General == null)
                return ValidateOptionsResult.Fail("General options section is required");

            if (options.Monitoring == null)
                return ValidateOptionsResult.Fail("Monitoring options section is required");

            if (options.Database == null)
                return ValidateOptionsResult.Fail("Database options section is required");

            if (options.Alerting == null)
                return ValidateOptionsResult.Fail("Alerting options section is required");

            if (options.CentralServer == null)
                return ValidateOptionsResult.Fail("CentralServer options section is required");

            // Validate central server consistency
            if (options.CentralServer.Enabled)
            {
                if (options.CentralServer.Mode.Equals("Agent", StringComparison.OrdinalIgnoreCase))
                {
                    if (string.IsNullOrWhiteSpace(options.CentralServer.ServerUrl))
                        return ValidateOptionsResult.Fail("CentralServer.ServerUrl is required when Mode is 'Agent'");
                }
                else if (options.CentralServer.Mode.Equals("Server", StringComparison.OrdinalIgnoreCase))
                {
                    if (string.IsNullOrWhiteSpace(options.CentralServer.CentralConnectionString))
                        return ValidateOptionsResult.Fail("CentralServer.CentralConnectionString is required when Mode is 'Server'");
                }
            }

            // Validate alerting consistency
            if (options.Alerting.EnableNotifications)
            {
                bool hasValidSender = (options.Alerting.Smtp.Enabled && 
                                      !string.IsNullOrWhiteSpace(options.Alerting.Smtp.Host)) ||
                                     (options.Alerting.GroupMe.Enabled && 
                                      !string.IsNullOrWhiteSpace(options.Alerting.GroupMe.BotId));

                if (!hasValidSender)
                    return ValidateOptionsResult.Fail(
                        "EnableNotifications is true but no valid alert sender is configured (SMTP or GroupMe)");
            }

            return ValidateOptionsResult.Success;
        }
    }

    /// <summary>
    /// Validates MonitoringOptions configuration.
    /// Ensures drives list is populated and threshold is reasonable.
    /// </summary>
    public class MonitoringOptionsValidator : IValidateOptions<MonitoringOptions>
    {
        /// <summary>
        /// Validates MonitoringOptions.
        /// </summary>
        public ValidateOptionsResult Validate(string? name, MonitoringOptions options)
        {
            if (options == null)
                return ValidateOptionsResult.Fail("MonitoringOptions is null");

            if (options.Drives == null || options.Drives.Count == 0)
                return ValidateOptionsResult.Fail("At least one drive must be specified for monitoring");

            // Validate drive letters format
            foreach (var drive in options.Drives)
            {
                if (string.IsNullOrWhiteSpace(drive) || drive.Length < 1)
                    return ValidateOptionsResult.Fail($"Invalid drive specification: '{drive}'");

                // Basic validation: drive should be like "C:" or "D:"
                if (!System.Text.RegularExpressions.Regex.IsMatch(drive, @"^[A-Z]:$"))
                    return ValidateOptionsResult.Fail($"Drive must be in format 'X:' where X is a letter: '{drive}'");
            }

            if (options.ThresholdPercent < 1 || options.ThresholdPercent > 100)
                return ValidateOptionsResult.Fail("ThresholdPercent must be between 1 and 100");

            return ValidateOptionsResult.Success;
        }
    }

    /// <summary>
    /// Validates SmtpOptions configuration.
    /// Ensures SMTP settings are complete if enabled.
    /// </summary>
    public class SmtpOptionsValidator : IValidateOptions<SmtpOptions>
    {
        /// <summary>
        /// Validates SmtpOptions.
        /// </summary>
        public ValidateOptionsResult Validate(string? name, SmtpOptions options)
        {
            if (options == null)
                return ValidateOptionsResult.Fail("SmtpOptions is null");

            if (!options.Enabled)
                return ValidateOptionsResult.Success;

            // If SMTP is enabled, all required fields must be populated
            if (string.IsNullOrWhiteSpace(options.Host))
                return ValidateOptionsResult.Fail("SMTP Host is required when SMTP is enabled");

            if (options.Port < 1 || options.Port > 65535)
                return ValidateOptionsResult.Fail("SMTP Port must be between 1 and 65535");

            if (string.IsNullOrWhiteSpace(options.FromAddress))
                return ValidateOptionsResult.Fail("SMTP FromAddress is required when SMTP is enabled");

            if (string.IsNullOrWhiteSpace(options.ToAddress))
                return ValidateOptionsResult.Fail("SMTP ToAddress is required when SMTP is enabled");

            return ValidateOptionsResult.Success;
        }
    }

    /// <summary>
    /// Validates GroupMeOptions configuration.
    /// Ensures BotId is provided if GroupMe is enabled.
    /// </summary>
    public class GroupMeOptionsValidator : IValidateOptions<GroupMeOptions>
    {
        /// <summary>
        /// Validates GroupMeOptions.
        /// </summary>
        public ValidateOptionsResult Validate(string? name, GroupMeOptions options)
        {
            if (options == null)
                return ValidateOptionsResult.Fail("GroupMeOptions is null");

            if (!options.Enabled)
                return ValidateOptionsResult.Success;

            if (string.IsNullOrWhiteSpace(options.BotId))
                return ValidateOptionsResult.Fail("GroupMe BotId is required when GroupMe is enabled");

            return ValidateOptionsResult.Success;
        }
    }

    /// <summary>
    /// Validates CentralServerOptions configuration.
    /// Ensures mode-specific requirements are met.
    /// </summary>
    public class CentralServerOptionsValidator : IValidateOptions<CentralServerOptions>
    {
        /// <summary>
        /// Validates CentralServerOptions.
        /// </summary>
        public ValidateOptionsResult Validate(string? name, CentralServerOptions options)
        {
            if (options == null)
                return ValidateOptionsResult.Fail("CentralServerOptions is null");

            if (!options.Enabled)
                return ValidateOptionsResult.Success;

            if (string.IsNullOrWhiteSpace(options.Mode))
                return ValidateOptionsResult.Fail("CentralServer Mode is required when enabled");

            if (options.Mode.Equals("Agent", StringComparison.OrdinalIgnoreCase))
            {
                if (string.IsNullOrWhiteSpace(options.ServerUrl))
                    return ValidateOptionsResult.Fail("ServerUrl is required when Mode is 'Agent'");

                // Basic URL validation
                if (!Uri.TryCreate(options.ServerUrl, UriKind.Absolute, out _))
                    return ValidateOptionsResult.Fail($"ServerUrl is not a valid URL: '{options.ServerUrl}'");
            }
            else if (options.Mode.Equals("Server", StringComparison.OrdinalIgnoreCase))
            {
                if (string.IsNullOrWhiteSpace(options.CentralConnectionString))
                    return ValidateOptionsResult.Fail("CentralConnectionString is required when Mode is 'Server'");
            }
            else
            {
                return ValidateOptionsResult.Fail("Mode must be 'Agent' or 'Server'");
            }

            return ValidateOptionsResult.Success;
        }
    }
}
