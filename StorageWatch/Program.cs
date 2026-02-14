/// <summary>
/// StorageWatch Service Program Entry Point
/// 
/// This is the main entry point for the Windows Service. It configures the host builder,
/// sets up dependency injection, and registers the background Worker service that monitors
/// disk space and sends alerts.
/// </summary>

using StorageWatch.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

// Configure and build the host using the generic host builder with Windows Service integration
Host.CreateDefaultBuilder(args)
    // UseWindowsService() enables this application to run as a Windows Service
    .UseWindowsService()
    .ConfigureServices(services =>
    {
        // Register the Worker as a hosted background service that will run continuously
        services.AddHostedService<Worker>();
    })
    .Build()
    .Run();