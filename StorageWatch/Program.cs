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
using StorageWatch.Services.Logging;
using StorageWatch.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using System.IO;

// Configure and build the host using the generic host builder with Windows Service integration
var host = Host.CreateDefaultBuilder(args)
    // UseWindowsService() enables this application to run as a Windows Service
    .UseWindowsService()
    .ConfigureServices((context, services) =>
    {
        // Load and validate JSON configuration
        var baseDir = AppContext.BaseDirectory;
        var configPath = Path.Combine(baseDir, "StorageWatchConfig.json");

        // Load configuration with validation
        var options = JsonConfigLoader.LoadAndValidate(configPath);

        // Register options in the service container for dependency injection
        services.Configure<StorageWatchOptions>(cfg =>
        {
            cfg.General = options.General;
            cfg.Monitoring = options.Monitoring;
            cfg.Database = options.Database;
            cfg.Alerting = options.Alerting;
            cfg.CentralServer = options.CentralServer;
        });

        // Register option validators
        services.AddSingleton<IValidateOptions<StorageWatchOptions>, StorageWatchOptionsValidator>();
        services.AddSingleton<IValidateOptions<MonitoringOptions>, MonitoringOptionsValidator>();
        services.AddSingleton<IValidateOptions<SmtpOptions>, SmtpOptionsValidator>();
        services.AddSingleton<IValidateOptions<GroupMeOptions>, GroupMeOptionsValidator>();
        services.AddSingleton<IValidateOptions<CentralServerOptions>, CentralServerOptionsValidator>();

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

        // Register the Worker as a hosted background service that will run continuously
        services.AddHostedService<Worker>();
    })
    .Build();

await host.RunAsync();