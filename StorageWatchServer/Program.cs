using Microsoft.Extensions.Options;
using Microsoft.Extensions.Hosting.WindowsServices;
using StorageWatchServer.Config;
using StorageWatchServer.Middleware;
using StorageWatchServer.Models;
using StorageWatchServer.Services.AutoUpdate;
using StorageWatchServer.Services.Logging;
using StorageWatchServer.Server.Api;
using StorageWatchServer.Server.Data;
using StorageWatchServer.Server.Reporting;
using StorageWatchServer.Server.Services;
using System.IO;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

// Enable Windows Service hosting
builder.Host.UseWindowsService();

// Detect test environment and set base path accordingly
var isTestEnvironment = AppContext.GetData("IsTestEnvironment") as bool? == true;
string basePath;

if (isTestEnvironment)
{
    // In test mode, use a unique temp directory for this test instance
    basePath = Path.Combine(Path.GetTempPath(), "StorageWatchServerTests", Guid.NewGuid().ToString());
}
else
{
    // In production, use ProgramData
    var programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
    basePath = Path.Combine(programData, "StorageWatch", "Server");
}

Directory.CreateDirectory(basePath);
var configPath = Path.Combine(basePath, "ServerConfig.json");
var dbPath = Path.Combine(basePath, "StorageWatchServer.db");

if (!File.Exists(configPath))
{
    // Auto-generate default config file on first run
    var defaultConfigPath = Path.Combine(AppContext.BaseDirectory, "Defaults", "ServerConfig.default.json");
    if (File.Exists(defaultConfigPath))
    {
        try
        {
            File.Copy(defaultConfigPath, configPath, overwrite: false);
            
            // Log config creation
            if (!isTestEnvironment)
            {
                var logFilePath = LogDirectoryInitializer.GetLogFilePath("server.log");
                var tempLogger = new RollingFileLogger(logFilePath);
                tempLogger.Log("[STARTUP] Default ServerConfig.json created at: " + configPath);
            }
        }
        catch (IOException)
        {
            // Another test host created it first — safe to ignore
        }
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

// Register RollingFileLogger for server logging (production mode only)
RollingFileLogger? rollingLogger = null;
if (!isTestEnvironment)
{
    var logFilePath = LogDirectoryInitializer.GetLogFilePath("server.log");
    rollingLogger = new RollingFileLogger(logFilePath);
    builder.Services.AddSingleton(rollingLogger);
}
// In test mode, do NOT register the logger — services will receive null

builder.Services.AddSingleton<ServerSchema>();
builder.Services.AddSingleton<ServerRepository>();
builder.Services.AddSingleton<RawRowIngestionService>();
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

// Use exception handler middleware
app.UseExceptionHandlerMiddleware();

// Log server startup
var appLogger = app.Services.GetRequiredService<ILogger<Program>>();
var runtimeRollingLogger = app.Services.GetService<RollingFileLogger>();

if (runtimeRollingLogger != null)
{
    runtimeRollingLogger.Log("[STARTUP] StorageWatch Server starting...");
    runtimeRollingLogger.Log($"[STARTUP] Config path: {configPath}");
    runtimeRollingLogger.Log($"[STARTUP] Database path: {dbPath}");
    runtimeRollingLogger.Log($"[STARTUP] Listen URL: {serverOptions.ListenUrl}");
}

appLogger.LogInformation("StorageWatch Server starting in server mode...");
appLogger.LogInformation("Server listening on: {ListenUrl}", serverOptions.ListenUrl);
appLogger.LogInformation("Database path: {DatabasePath}", serverOptions.DatabasePath);
appLogger.LogInformation("Online timeout: {TimeoutMinutes} minutes", serverOptions.OnlineTimeoutMinutes);

var schema = app.Services.GetRequiredService<ServerSchema>();
try
{
    await schema.InitializeDatabaseAsync();
    appLogger.LogInformation("Database initialized successfully");
    
    if (runtimeRollingLogger != null)
    {
        runtimeRollingLogger.Log("[STARTUP] Database initialized successfully");
    }
}
catch (Exception ex)
{
    appLogger.LogError(ex, "Failed to initialize database");
    
    if (runtimeRollingLogger != null)
    {
        runtimeRollingLogger.Log($"[ERROR] Failed to initialize database: {ex.Message}");
    }
    throw;
}

app.UseStaticFiles();

app.MapControllers();
app.MapRazorPages();

if (runtimeRollingLogger != null)
{
    runtimeRollingLogger.Log("[STARTUP] StorageWatch Server ready to accept connections");
}
appLogger.LogInformation("StorageWatch Server ready to accept connections");

app.Run();
