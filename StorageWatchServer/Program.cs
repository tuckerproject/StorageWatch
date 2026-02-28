using Microsoft.Extensions.Options;
using Microsoft.Extensions.Hosting.WindowsServices;
using StorageWatchServer.Config;
using StorageWatchServer.Models;
using StorageWatchServer.Services.AutoUpdate;
using StorageWatchServer.Server.Api;
using StorageWatchServer.Server.Data;
using StorageWatchServer.Server.Reporting.Data;
using StorageWatchServer.Server.Services;
using System.IO;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

// Enable Windows Service hosting
builder.Host.UseWindowsService();

// Load and validate JSON configuration from ServerConfig.json
var programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
var serverDirectory = Path.Combine(programData, "StorageWatch", "Server");
Directory.CreateDirectory(serverDirectory);
var configPath = Path.Combine(serverDirectory, "ServerConfig.json");

if (!File.Exists(configPath))
{
    // Auto-generate default config file on first run
    var defaultConfigPath = Path.Combine(AppContext.BaseDirectory, "Defaults", "ServerConfig.default.json");
    if (File.Exists(defaultConfigPath))
    {
        File.Copy(defaultConfigPath, configPath, overwrite: false);
        var tempLogger = LoggerFactory.Create(x => x.AddConsole()).CreateLogger<Program>();
        tempLogger.LogInformation("Default ServerConfig.json created at: {ConfigPath}", configPath);
    }
}

// Load configuration from ServerConfig.json
var configBuilder = new ConfigurationBuilder()
    .AddJsonFile(configPath, optional: false, reloadOnChange: false)
    .Build();

builder.Configuration.AddConfiguration(configBuilder);

builder.Services.AddRazorPages(options =>
{
    options.RootDirectory = "/Dashboard";
});

builder.Services.AddControllers();

builder.Services.Configure<ServerOptions>(builder.Configuration.GetSection("Server"));
builder.Services.Configure<AutoUpdateOptions>(builder.Configuration.GetSection(AutoUpdateOptions.SectionKey));
builder.Services.AddSingleton(sp => sp.GetRequiredService<IOptions<ServerOptions>>().Value);
builder.Services.AddSingleton<ServerSchema>();
builder.Services.AddSingleton<ServerRepository>();
builder.Services.AddSingleton<AgentReportSchema>();
builder.Services.AddSingleton<IAgentReportRepository, AgentReportRepository>();
builder.Services.AddSingleton<MachineStatusService>();

builder.Services.AddHttpClient<IServerUpdateChecker, ServerUpdateChecker>();
builder.Services.AddHttpClient<IServerUpdateDownloader, ServerUpdateDownloader>();
builder.Services.AddSingleton<IServerRestartHandler, ServerRestartHandler>();
builder.Services.AddSingleton<IServerUpdateInstaller, ServerUpdateInstaller>();
builder.Services.AddSingleton<IAutoUpdateTimerFactory, AutoUpdateTimerFactory>();
builder.Services.AddHostedService<ServerAutoUpdateWorker>();

var serverOptions = builder.Configuration.GetSection("Server").Get<ServerOptions>() ?? new ServerOptions();
if (!string.IsNullOrWhiteSpace(serverOptions.ListenUrl))
{
    builder.WebHost.UseUrls(serverOptions.ListenUrl);
}

var app = builder.Build();

// Log server startup
var appLogger = app.Services.GetRequiredService<ILogger<Program>>();
appLogger.LogInformation("StorageWatch Server starting in server mode...");
appLogger.LogInformation("Server listening on: {ListenUrl}", serverOptions.ListenUrl);
appLogger.LogInformation("Database path: {DatabasePath}", serverOptions.DatabasePath);
appLogger.LogInformation("Agent report database path: {AgentReportDatabasePath}", serverOptions.AgentReportDatabasePath);
appLogger.LogInformation("Online timeout: {TimeoutMinutes} minutes", serverOptions.OnlineTimeoutMinutes);

var schema = app.Services.GetRequiredService<ServerSchema>();
try
{
    await schema.InitializeDatabaseAsync();
    appLogger.LogInformation("Database initialized successfully");
}
catch (Exception ex)
{
    appLogger.LogError(ex, "Failed to initialize database");
    throw;
}

var reportSchema = app.Services.GetRequiredService<AgentReportSchema>();
try
{
    await reportSchema.InitializeDatabaseAsync();
    appLogger.LogInformation("Agent report database initialized successfully");
}
catch (Exception ex)
{
    appLogger.LogError(ex, "Failed to initialize agent report database");
    throw;
}

app.UseStaticFiles();

app.MapControllers();
app.MapRazorPages();

var apiGroup = app.MapGroup("/api");
apiGroup.MapAgentEndpoints();

appLogger.LogInformation("StorageWatch Server ready to accept connections");

app.Run();
