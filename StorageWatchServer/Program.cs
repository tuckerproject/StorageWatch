using Microsoft.Extensions.Options;
using StorageWatchServer.Server.Api;
using StorageWatchServer.Server.Data;
using StorageWatchServer.Server.Reporting.Data;
using StorageWatchServer.Server.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorPages(options =>
{
    options.RootDirectory = "/Dashboard";
});

builder.Services.Configure<ServerOptions>(builder.Configuration.GetSection("Server"));
builder.Services.AddSingleton(sp => sp.GetRequiredService<IOptions<ServerOptions>>().Value);
builder.Services.AddSingleton<ServerSchema>();
builder.Services.AddSingleton<ServerRepository>();
builder.Services.AddSingleton<AgentReportSchema>();
builder.Services.AddSingleton<IAgentReportRepository, AgentReportRepository>();
builder.Services.AddSingleton<MachineStatusService>();

var serverOptions = builder.Configuration.GetSection("Server").Get<ServerOptions>() ?? new ServerOptions();
if (!string.IsNullOrWhiteSpace(serverOptions.ListenUrl))
{
    builder.WebHost.UseUrls(serverOptions.ListenUrl);
}

var app = builder.Build();

// Log server startup
var logger = app.Services.GetRequiredService<ILogger<Program>>();
logger.LogInformation("StorageWatch Server starting in server mode...");
logger.LogInformation("Server listening on: {ListenUrl}", serverOptions.ListenUrl);
logger.LogInformation("Database path: {DatabasePath}", serverOptions.DatabasePath);
logger.LogInformation("Agent report database path: {AgentReportDatabasePath}", serverOptions.AgentReportDatabasePath);
logger.LogInformation("Online timeout: {TimeoutMinutes} minutes", serverOptions.OnlineTimeoutMinutes);

var schema = app.Services.GetRequiredService<ServerSchema>();
try
{
    await schema.InitializeDatabaseAsync();
    logger.LogInformation("Database initialized successfully");
}
catch (Exception ex)
{
    logger.LogError(ex, "Failed to initialize database");
    throw;
}

var reportSchema = app.Services.GetRequiredService<AgentReportSchema>();
try
{
    await reportSchema.InitializeDatabaseAsync();
    logger.LogInformation("Agent report database initialized successfully");
}
catch (Exception ex)
{
    logger.LogError(ex, "Failed to initialize agent report database");
    throw;
}

app.UseStaticFiles();

app.MapRazorPages();

var apiGroup = app.MapGroup("/api");
apiGroup.MapAgentEndpoints();

logger.LogInformation("StorageWatch Server ready to accept connections");

app.Run();
