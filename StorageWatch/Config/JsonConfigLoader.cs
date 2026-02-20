/// <summary>
/// JSON Configuration Loader for StorageWatch
/// 
/// Replaces the legacy XML ConfigLoader with a modern JSON-based approach using
/// Microsoft.Extensions.Configuration and the Options pattern.
/// Provides utilities for loading, binding, and validating configuration from JSON files.
/// </summary>

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using StorageWatch.Config.Encryption;
using StorageWatch.Config.Options;
using System;
using System.Collections.Generic;
using System.IO;

namespace StorageWatch.Config
{
    /// <summary>
    /// Result of configuration validation.
    /// </summary>
    public class ValidationResult
    {
        public bool IsValid { get; set; }
        public List<string> Errors { get; set; } = new();
        public List<string> Warnings { get; set; } = new();
    }

    /// <summary>
    /// Utility class for loading JSON configuration files and binding them to strongly-typed options.
    /// </summary>
    public static class JsonConfigLoader
    {
        /// <summary>
        /// Loads JSON configuration from a file and returns a bound StorageWatchOptions object.
        /// Validates the configuration using registered validators.
        /// </summary>
        /// <param name="configPath">Path to the StorageWatchConfig.json file.</param>
        /// <param name="encryptor">Optional encryptor for decrypting sensitive fields. Defaults to no-op.</param>
        /// <returns>A validated StorageWatchOptions object.</returns>
        /// <exception cref="FileNotFoundException">Thrown if the configuration file does not exist.</exception>
        /// <exception cref="InvalidOperationException">Thrown if configuration validation fails.</exception>
        public static StorageWatchOptions LoadAndValidate(string configPath, IConfigurationEncryptor? encryptor = null)
        {
            if (!File.Exists(configPath))
                throw new FileNotFoundException($"Configuration file not found: {configPath}");

            encryptor ??= new NoOpConfigurationEncryptor();

            // Build configuration from JSON file
            var config = new ConfigurationBuilder()
                .AddJsonFile(configPath, optional: false, reloadOnChange: true)
                .Build();

            // Bind to strongly typed options
            var options = new StorageWatchOptions();
            config.GetSection(StorageWatchOptions.SectionKey).Bind(options);

            // Decrypt sensitive fields if necessary
            DecryptSensitiveFields(options, encryptor);

            // Validate configuration
            ValidateOptions(options);

            return options;
        }

        /// <summary>
        /// Creates an IOptionsMonitor for configuration reload-on-change support.
        /// Useful when integrated with dependency injection.
        /// </summary>
        /// <param name="configPath">Path to the StorageWatchConfig.json file.</param>
        /// <param name="encryptor">Optional encryptor for decrypting sensitive fields.</param>
        /// <returns>An IOptionsMonitor that automatically reloads configuration when file changes.</returns>
        public static IOptionsMonitor<StorageWatchOptions> CreateOptionsMonitor(
            string configPath,
            IConfigurationEncryptor? encryptor = null)
        {
            if (!File.Exists(configPath))
                throw new FileNotFoundException($"Configuration file not found: {configPath}");

            encryptor ??= new NoOpConfigurationEncryptor();

            var config = new ConfigurationBuilder()
                .AddJsonFile(configPath, optional: false, reloadOnChange: true)
                .Build();

            var services = new Microsoft.Extensions.DependencyInjection.ServiceCollection();
            services.Configure<StorageWatchOptions>(cfg =>
            {
                config.GetSection(StorageWatchOptions.SectionKey).Bind(cfg);
                DecryptSensitiveFields(cfg, encryptor);
            });

            services.AddSingleton<IValidateOptions<StorageWatchOptions>, StorageWatchOptionsValidator>();
            services.AddSingleton<IValidateOptions<MonitoringOptions>, MonitoringOptionsValidator>();
            services.AddSingleton<IValidateOptions<SmtpOptions>, SmtpOptionsValidator>();
            services.AddSingleton<IValidateOptions<GroupMeOptions>, GroupMeOptionsValidator>();
            services.AddSingleton<IValidateOptions<CentralServerOptions>, CentralServerOptionsValidator>();

            var provider = services.BuildServiceProvider();
            return provider.GetRequiredService<IOptionsMonitor<StorageWatchOptions>>();
        }

        /// <summary>
        /// Validates a configuration file and returns the validation result.
        /// Does not throw exceptions on validation failure.
        /// </summary>
        /// <param name="configPath">Path to the StorageWatchConfig.json file.</param>
        /// <returns>A validation result object containing errors and warnings.</returns>
        public static ValidationResult Validate(string configPath)
        {
            var result = new ValidationResult { IsValid = true };

            try
            {
                if (!File.Exists(configPath))
                {
                    result.IsValid = false;
                    result.Errors.Add($"Configuration file not found: {configPath}");
                    return result;
                }

                // Try to parse JSON
                var config = new ConfigurationBuilder()
                    .AddJsonFile(configPath, optional: false, reloadOnChange: false)
                    .Build();

                // Bind to options
                var options = new StorageWatchOptions();
                config.GetSection(StorageWatchOptions.SectionKey).Bind(options);

                // Run validators
                var validators = new IValidateOptions<StorageWatchOptions>[]
                {
                    new StorageWatchOptionsValidator(),
                };

                foreach (var validator in validators)
                {
                    var validationResult = validator.Validate(null, options);
                    if (!validationResult.Succeeded)
                    {
                        result.IsValid = false;
                        result.Errors.Add(validationResult.FailureMessage ?? "Unknown validation error");
                    }
                }

                // Validate sub-options
                ValidateSubOptionsWithResult(options, result);
            }
            catch (Exception ex)
            {
                result.IsValid = false;
                result.Errors.Add($"Configuration validation failed: {ex.Message}");
            }

            return result;
        }

        /// <summary>
        /// Validates sub-options and adds errors/warnings to the result.
        /// </summary>
        private static void ValidateSubOptionsWithResult(StorageWatchOptions options, ValidationResult result)
        {
            var monitoringValidator = new MonitoringOptionsValidator();
            var validationResult = monitoringValidator.Validate(null, options.Monitoring);
            if (!validationResult.Succeeded)
            {
                result.IsValid = false;
                result.Errors.Add($"Monitoring: {validationResult.FailureMessage}");
            }

            var smtpValidator = new SmtpOptionsValidator();
            validationResult = smtpValidator.Validate(null, options.Alerting.Smtp);
            if (!validationResult.Succeeded)
            {
                if (options.Alerting.Smtp.Enabled)
                {
                    result.IsValid = false;
                    result.Errors.Add($"SMTP: {validationResult.FailureMessage}");
                }
                else
                {
                    result.Warnings.Add($"SMTP: {validationResult.FailureMessage} (disabled, ignoring)");
                }
            }

            var groupMeValidator = new GroupMeOptionsValidator();
            validationResult = groupMeValidator.Validate(null, options.Alerting.GroupMe);
            if (!validationResult.Succeeded)
            {
                if (options.Alerting.GroupMe.Enabled)
                {
                    result.IsValid = false;
                    result.Errors.Add($"GroupMe: {validationResult.FailureMessage}");
                }
                else
                {
                    result.Warnings.Add($"GroupMe: {validationResult.FailureMessage} (disabled, ignoring)");
                }
            }

            var centralServerValidator = new CentralServerOptionsValidator();
            validationResult = centralServerValidator.Validate(null, options.CentralServer);
            if (!validationResult.Succeeded)
            {
                if (options.CentralServer.Enabled)
                {
                    result.Warnings.Add($"Central Server: {validationResult.FailureMessage}");
                }
            }
        }

        /// <summary>
        /// Validates StorageWatchOptions using registered validators.
        /// </summary>
        /// <param name="options">The options to validate.</param>
        /// <exception cref="InvalidOperationException">Thrown if validation fails.</exception>
        private static void ValidateOptions(StorageWatchOptions options)
        {
            var validators = new IValidateOptions<StorageWatchOptions>[]
            {
                new StorageWatchOptionsValidator(),
            };

            foreach (var validator in validators)
            {
                var result = validator.Validate(null, options);
                if (!result.Succeeded)
                    throw new InvalidOperationException($"Configuration validation failed: {result.FailureMessage}");
            }

            // Validate sub-options
            ValidateSubOptions(options);
        }

        /// <summary>
        /// Validates all sub-options sections.
        /// </summary>
        private static void ValidateSubOptions(StorageWatchOptions options)
        {
            var monitoringValidator = new MonitoringOptionsValidator();
            var result = monitoringValidator.Validate(null, options.Monitoring);
            if (!result.Succeeded)
                throw new InvalidOperationException($"Monitoring configuration validation failed: {result.FailureMessage}");

            var smtpValidator = new SmtpOptionsValidator();
            result = smtpValidator.Validate(null, options.Alerting.Smtp);
            if (!result.Succeeded)
                throw new InvalidOperationException($"SMTP configuration validation failed: {result.FailureMessage}");

            var groupMeValidator = new GroupMeOptionsValidator();
            result = groupMeValidator.Validate(null, options.Alerting.GroupMe);
            if (!result.Succeeded)
                throw new InvalidOperationException($"GroupMe configuration validation failed: {result.FailureMessage}");

            var centralServerValidator = new CentralServerOptionsValidator();
            result = centralServerValidator.Validate(null, options.CentralServer);
            if (!result.Succeeded)
                throw new InvalidOperationException($"Central server configuration validation failed: {result.FailureMessage}");
        }

        /// <summary>
        /// Decrypts sensitive configuration fields using the provided encryptor.
        /// </summary>
        private static void DecryptSensitiveFields(StorageWatchOptions options, IConfigurationEncryptor encryptor)
        {
            // Decrypt SMTP credentials
            if (!string.IsNullOrEmpty(options.Alerting.Smtp.Password))
                options.Alerting.Smtp.Password = encryptor.Decrypt(options.Alerting.Smtp.Password);

            if (!string.IsNullOrEmpty(options.Alerting.Smtp.Username))
                options.Alerting.Smtp.Username = encryptor.Decrypt(options.Alerting.Smtp.Username);

            // Decrypt GroupMe BotId
            if (!string.IsNullOrEmpty(options.Alerting.GroupMe.BotId))
                options.Alerting.GroupMe.BotId = encryptor.Decrypt(options.Alerting.GroupMe.BotId);

            // Decrypt Central Server API Key
            if (!string.IsNullOrEmpty(options.CentralServer.ApiKey))
                options.CentralServer.ApiKey = encryptor.Decrypt(options.CentralServer.ApiKey);
        }
    }
}
