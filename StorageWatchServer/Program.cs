using Microsoft.Extensions.Options;
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

// Check operational mode before proceeding
var programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
var storageWatchDirectory = Path.Combine(programData, "StorageWatch");
var configPath = Path.Combine(storageWatchDirectory, "StorageWatchConfig.json");

string? currentMode = "Server"; // Default to Server
if (File.Exists(configPath))
{
    try
    {
        using (var stream = File.OpenRead(configPath))
        {
            using (var jsonDoc = await JsonDocument.ParseAsync(stream))
            {
                var root = jsonDoc.RootElement;
                if (root.TryGetProperty("StorageWatch", out var swElement))
                {
                    if (swElement.TryGetProperty("Mode", out var modeElement))
                    {
                        currentMode = modeElement.GetString();
                    }
                }
            }
        }
    }
    catch
    {
        // If config cannot be parsed, proceed with default Server mode
    }
}

// If mode is not Server, exit gracefully
if (currentMode != "Server" && !string.IsNullOrEmpty(currentMode))
{
    var logger = LoggerFactory.Create(x => x.AddConsole()).CreateLogger<Program>();
    logger.LogError("StorageWatch Server can only run in 'Server' mode. Current mode: {Mode}", currentMode ?? "Unknown");
    logger.LogError("To run in Agent mode, use StorageWatchService.exe");
    logger.LogError("To run in Standalone mode, use StorageWatchService.exe");
    Environment.Exit(1);
}

builder.Services.AddRazorPages(options =>
{
    options.RootDirectory = "/Dashboard";
});

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

app.MapRazorPages();

var apiGroup = app.MapGroup("/api");
apiGroup.MapAgentEndpoints();

appLogger.LogInformation("StorageWatch Server ready to accept connections");

app.Run();
