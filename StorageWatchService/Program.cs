/// <summary>
/// StorageWatch Service Program Entry Point
/// 
/// This is the main entry point for the Windows Service. It configures the host builder,
/// sets up dependency injection, and registers the background Worker service that monitors
/// disk space and sends alerts.
/// 
/// Now includes plugin architecture for extensible alert senders.
/// </summary>

using StorageWatch.Config;
using StorageWatch.Config.Options;
using StorageWatch.Services;
using StorageWatch.Services.Alerting;
using StorageWatch.Services.Alerting.Plugins;
using StorageWatch.Services.CentralServer;
using StorageWatch.Services.Logging;
using StorageWatch.Services.Monitoring;
using StorageWatch.Models;
using StorageWatch.Communication;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using System.IO;
using System.Collections.Generic;

// Configure and build the host using the generic host builder with Windows Service integration
var host = Host.CreateDefaultBuilder(args)
    // UseWindowsService() enables this application to run as a Windows Service
    .UseWindowsService()
    .ConfigureServices((context, services) =>
    {
        // Load and validate JSON configuration
        var programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
        var storageWatchDirectory = Path.Combine(programData, "StorageWatch");
        Directory.CreateDirectory(storageWatchDirectory);
        var configPath = Path.Combine(storageWatchDirectory, "StorageWatchConfig.json");

        StorageWatchOptions options;
        if (File.Exists(configPath))
        {
            options = JsonConfigLoader.LoadAndValidate(configPath);
        }
        else
        {
            options = CreateDefaultOptions();
        }

        var databasePath = Path.Combine(storageWatchDirectory, "StorageWatch.db");
        options.Database.ConnectionString = $"Data Source={databasePath}";

        // Register options in the service container for dependency injection
        services.Configure<StorageWatchOptions>(cfg =>
        {
            cfg.General = options.General;
            cfg.Monitoring = options.Monitoring;
            cfg.Database = options.Database;
            cfg.Alerting = options.Alerting;
        });

        services.Configure<CentralServerOptions>(context.Configuration.GetSection(CentralServerOptions.SectionKey));

        // Register option validators
        services.AddSingleton<IValidateOptions<StorageWatchOptions>, StorageWatchOptionsValidator>();
        services.AddSingleton<IValidateOptions<MonitoringOptions>, MonitoringOptionsValidator>();
        services.AddSingleton<IValidateOptions<SmtpOptions>, SmtpOptionsValidator>();
        services.AddSingleton<IValidateOptions<GroupMeOptions>, GroupMeOptionsValidator>();
        services.AddSingleton<IValidateOptions<CentralServerOptions>, CentralServerOptionsValidator>();

        // Register the logger
        var logFilePath = Path.Combine(programData, "StorageWatch", "Logs", "service.log");
        services.AddSingleton(new RollingFileLogger(logFilePath));

        // ====================================================================
        // Plugin Architecture Registration
        // ====================================================================

        // Create and configure the plugin registry
        var registry = new AlertSenderPluginRegistry();
        registry.DiscoverPlugins(); // Discover all alert sender plugins in current assembly
        services.AddSingleton(registry);

        // Register individual alert sender plugins with their options
        services.AddTransient<SmtpAlertSender>(sp =>
            new SmtpAlertSender(
                options.Alerting.Smtp,
                sp.GetRequiredService<RollingFileLogger>()));

        services.AddTransient<GroupMeAlertSender>(sp =>
            new GroupMeAlertSender(
                options.Alerting.GroupMe,
                sp.GetRequiredService<RollingFileLogger>()));

        // Register plugins as IAlertSender for plugin manager resolution
        services.AddTransient<IAlertSender, SmtpAlertSender>(sp =>
            new SmtpAlertSender(
                options.Alerting.Smtp,
                sp.GetRequiredService<RollingFileLogger>()));

        services.AddTransient<IAlertSender, GroupMeAlertSender>(sp =>
            new GroupMeAlertSender(
                options.Alerting.GroupMe,
                sp.GetRequiredService<RollingFileLogger>()));

        // Register the plugin manager
        services.AddSingleton<AlertSenderPluginManager>();

        // ====================================================================
        // End Plugin Architecture Registration
        // ====================================================================

        // ====================================================================
        // IPC Communication Server Registration
        // ====================================================================

        // Register the IPC server as a hosted service for UI communication
        services.AddHostedService<ServiceCommunicationServer>();

        // ====================================================================
        // End IPC Communication Server Registration
        // ====================================================================

        services.AddSingleton<IDiskStatusProvider>(sp =>
            new DiskAlertMonitor(sp.GetRequiredService<IOptionsMonitor<StorageWatchOptions>>().CurrentValue));
        services.AddSingleton<AgentReportBuilder>();
        services.AddHttpClient();
        services.AddHttpClient<AgentReportSender>();
        services.AddHostedService<AgentReportWorker>();

        // Register the Worker as a hosted background service that will run continuously
        services.AddHostedService<Worker>();

        StorageWatchOptions CreateDefaultOptions()
        {
            var defaultOptions = new StorageWatchOptions();
            defaultOptions.Alerting.EnableNotifications = false;

            var systemDrive = Path.GetPathRoot(Environment.SystemDirectory);
            var driveLetter = string.IsNullOrWhiteSpace(systemDrive)
                ? "C:"
                : systemDrive.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

            defaultOptions.Monitoring.Drives = new List<string> { driveLetter };
            return defaultOptions;
        }
    })
    .Build();

await host.RunAsync();