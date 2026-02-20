using Microsoft.Extensions.Options;
using StorageWatchServer.Server.Api;
using StorageWatchServer.Server.Data;
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
builder.Services.AddSingleton<MachineStatusService>();

var serverOptions = builder.Configuration.GetSection("Server").Get<ServerOptions>() ?? new ServerOptions();
if (!string.IsNullOrWhiteSpace(serverOptions.ListenUrl))
{
    builder.WebHost.UseUrls(serverOptions.ListenUrl);
}

var app = builder.Build();

var schema = app.Services.GetRequiredService<ServerSchema>();
await schema.InitializeDatabaseAsync();

app.UseStaticFiles();

app.MapRazorPages();

var apiGroup = app.MapGroup("/api");
apiGroup.MapAgentEndpoints();

app.Run();
