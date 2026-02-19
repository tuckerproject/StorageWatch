using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StorageWatchUI.Services;
using StorageWatchUI.ViewModels;
using System.IO;
using System.Windows;

namespace StorageWatchUI;

public partial class App : Application
{
    public IServiceProvider ServiceProvider { get; private set; } = null!;
    public IConfiguration Configuration { get; private set; } = null!;

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

        // Show main window
        var mainWindow = ServiceProvider.GetRequiredService<MainWindow>();
        mainWindow.Show();
    }

    private void ConfigureServices(IServiceCollection services)
    {
        // Configuration
        services.AddSingleton(Configuration);

        // Path provider (runtime path resolution)
        services.AddSingleton<IPathProvider, PathProvider>();

        // Services
        services.AddSingleton<IDataProvider, LocalDataProvider>();
        services.AddSingleton<CentralDataProvider>();
        services.AddSingleton<IServiceManager, ServiceManager>();
        services.AddSingleton<ConfigurationService>();

        // ViewModels
        services.AddSingleton<MainViewModel>();
        services.AddTransient<DashboardViewModel>();
        services.AddTransient<TrendsViewModel>();
        services.AddTransient<CentralViewModel>();
        services.AddTransient<SettingsViewModel>();
        services.AddTransient<ServiceStatusViewModel>();

        // Views
        services.AddSingleton<MainWindow>();
    }

    private static string GetStorageWatchConfigPath()
    {
        var programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
        var configPath = Path.Combine(programData, "StorageWatch", "StorageWatchConfig.json");
        
        // Fallback to current directory if not found
        if (!File.Exists(configPath))
        {
            configPath = Path.Combine(Directory.GetCurrentDirectory(), "StorageWatchConfig.json");
        }

        return configPath;
    }
}
