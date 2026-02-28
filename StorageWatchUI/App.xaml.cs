using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StorageWatchUI.Config;
using StorageWatchUI.Services;
using StorageWatchUI.Services.AutoUpdate;
using StorageWatchUI.ViewModels;
using System.IO;
using System.Windows;

namespace StorageWatchUI;

public partial class App : Application
{
    public IServiceProvider ServiceProvider { get; private set; } = null!;
    public IConfiguration Configuration { get; private set; } = null!;
    private IUiAutoUpdateWorker? _autoUpdateWorker;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Build configuration
        var builder = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
            .AddJsonFile(GetStorageWatchConfigPath(), optional: true, reloadOnChange: true);

        Configuration = builder.Build();

        // Configure DI
        var serviceCollection = new ServiceCollection();
        ConfigureServices(serviceCollection);

        ServiceProvider = serviceCollection.BuildServiceProvider();

        _autoUpdateWorker = ServiceProvider.GetRequiredService<IUiAutoUpdateWorker>();
        _autoUpdateWorker.Start();

        // Show main window
        var mainWindow = ServiceProvider.GetRequiredService<MainWindow>();
        mainWindow.Show();

        // Trigger initial update check in the UI
        var updateViewModel = ServiceProvider.GetRequiredService<UpdateViewModel>();
        updateViewModel.CheckForUpdatesCommand.Execute(null);
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        if (_autoUpdateWorker != null)
        {
            await _autoUpdateWorker.StopAsync();
        }

        base.OnExit(e);
    }

    private void ConfigureServices(IServiceCollection services)
    {
        // Configuration
        services.AddSingleton(Configuration);
        services.AddLogging();
        services.Configure<AutoUpdateOptions>(Configuration.GetSection(AutoUpdateOptions.SectionKey));

        // Path provider (runtime path resolution)
        services.AddSingleton<IPathProvider, PathProvider>();

        // Services
        services.AddSingleton<IDataProvider, LocalDataProvider>();
        services.AddSingleton<IServiceManager, ServiceManager>();
        services.AddSingleton<ConfigurationService>();

        // Auto-Update Services
        services.AddHttpClient<IUiUpdateChecker, UiUpdateChecker>();
        services.AddHttpClient<IUiUpdateDownloader, UiUpdateDownloader>();
        services.AddSingleton<IUiRestartPrompter, UiRestartPrompter>();
        services.AddSingleton<IUiRestartHandler, UiRestartHandler>();
        services.AddSingleton<IUiUpdateInstaller, UiUpdateInstaller>();
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
