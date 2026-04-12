using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StorageWatchUI.Config;
using StorageWatchUI.Exceptions;
using StorageWatchUI.Services;
using StorageWatchUI.Services.AutoUpdate;
using StorageWatchUI.Services.Logging;
using StorageWatchUI.ViewModels;
using System.IO;
using System.Windows;

namespace StorageWatchUI;

public partial class App : Application
{
    public IServiceProvider ServiceProvider { get; private set; } = null!;
    public IConfiguration Configuration { get; private set; } = null!;
    private IUiAutoUpdateWorker? _autoUpdateWorker;
    private RollingFileLogger? _logger;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        try
        {
            // Initialize logging
            var logFilePath = LogDirectoryInitializer.GetLogFilePath("ui.log");
            _logger = new RollingFileLogger(logFilePath);
            _logger.Log("[STARTUP] StorageWatchUI starting...");

            // Register global exception handler
            UIExceptionHandler.Initialize(_logger);
            _logger.Log("[STARTUP] Global exception handler registered");

            // Build configuration
            var builder = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                .AddJsonFile(GetStorageWatchConfigPath(), optional: true, reloadOnChange: true);

            Configuration = builder.Build();
            _logger.Log("[STARTUP] Configuration loaded");

            // Configure DI
            var serviceCollection = new ServiceCollection();
            ConfigureServices(serviceCollection);

            ServiceProvider = serviceCollection.BuildServiceProvider();
            _logger.Log("[STARTUP] Services configured and DI container built");

            _autoUpdateWorker = ServiceProvider.GetRequiredService<IUiAutoUpdateWorker>();
            _autoUpdateWorker.Start();
            _logger.Log("[STARTUP] Auto-update worker started");

            // Show main window
            var mainWindow = ServiceProvider.GetRequiredService<MainWindow>();
            mainWindow.Show();
            _logger.Log("[STARTUP] Main window displayed");
        }
        catch (Exception ex)
        {
            _logger?.Log($"[ERROR] Startup failed: {ex.Message}");
            MessageBox.Show($"Failed to start StorageWatchUI: {ex.Message}", "Startup Error", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
        }
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        try
        {
            _logger?.Log("[STARTUP] StorageWatchUI shutting down...");
            
            if (_autoUpdateWorker != null)
            {
                await _autoUpdateWorker.StopAsync();
                _logger?.Log("[STARTUP] Auto-update worker stopped");
            }

            _logger?.Log("[STARTUP] StorageWatchUI shutdown complete");
        }
        catch (Exception ex)
        {
            _logger?.Log($"[ERROR] Error during shutdown: {ex.Message}");
        }

        base.OnExit(e);
    }

    private void ConfigureServices(IServiceCollection services)
    {
        // Configuration
        services.AddSingleton(Configuration);
        services.AddLogging();
        services.Configure<AutoUpdateOptions>(Configuration.GetSection(AutoUpdateOptions.SectionKey));

        // Logging
        if (_logger != null)
        {
            services.AddSingleton(_logger);
        }

        // Path provider (runtime path resolution)
        services.AddSingleton<IPathProvider, PathProvider>();

        // Services
        services.AddSingleton<IDataProvider, LocalDataProvider>();
        services.AddSingleton<IServiceManager, ServiceManager>();
        services.AddSingleton<ConfigurationService>();

        // Auto-Update Services
        services.AddHttpClient<IUiUpdateChecker, UiUpdateChecker>();
        services.AddHttpClient<IUiUpdateDownloader, UiUpdateDownloader>();
        services.AddSingleton<IUiUpdateInstaller, UiUpdateHandoffInstaller>();
        services.AddSingleton<IUiUpdateUserSettingsStore, UiUpdateUserSettingsStore>();
        services.AddSingleton<IAutoUpdateTimerFactory, AutoUpdateTimerFactory>();
        services.AddSingleton<IUiAutoUpdateWorker, UiAutoUpdateWorker>();

        // ViewModels
        services.AddSingleton<MainViewModel>();
        services.AddSingleton<UpdateViewModel>();
        services.AddTransient<DashboardViewModel>();
        services.AddTransient<TrendsViewModel>();
        services.AddTransient<SettingsViewModel>();
        services.AddTransient<ServiceStatusViewModel>();

        // Dialog ViewModels
        services.AddTransient<UpdateDialogViewModel>();
        services.AddTransient<UpdateProgressViewModel>();

        // Views
        services.AddSingleton<MainWindow>();
    }

    private static string GetStorageWatchConfigPath()
    {
        var programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
        var configPath = Path.Combine(programData, "StorageWatch", "Agent", "AgentConfig.json");
        return configPath;
    }
}
