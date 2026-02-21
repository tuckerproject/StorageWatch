using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using StorageWatchServer.Server.Data;
using StorageWatchServer.Server.Services;
using StorageWatchServer.Tests.Utilities;
using Xunit;

namespace StorageWatchServer.Tests.Pages;

public class DashboardPagesTests : IAsyncLifetime
{
    private WebApplicationFactory<Program>? _factory;
    private HttpClient? _client;
    private readonly string _testDatabaseId = Guid.NewGuid().ToString("N")[..8];

    public async Task InitializeAsync()
    {
        _factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureServices(services =>
                {
                    // Configure test-specific services with unique in-memory database
                    var serverOptions = new ServerOptions
                    {
                        ListenUrl = "http://localhost:5001",
                        DatabasePath = $"file:memdb_pages_{_testDatabaseId}?mode=memory&cache=shared",
                        OnlineTimeoutMinutes = 5
                    };

                    // Remove existing ServerOptions and add test version
                    var descriptor = services.FirstOrDefault(d => d.ServiceType == typeof(ServerOptions));
                    if (descriptor != null)
                    {
                        services.Remove(descriptor);
                    }

                    services.AddSingleton(serverOptions);
                    
                    // Remove and re-register ServerSchema
                    var schemaDescriptor = services.FirstOrDefault(d => d.ServiceType == typeof(ServerSchema));
                    if (schemaDescriptor != null)
                    {
                        services.Remove(schemaDescriptor);
                    }
                    services.AddSingleton<ServerSchema>();

                    // Remove and re-register ServerRepository
                    var repoDescriptor = services.FirstOrDefault(d => d.ServiceType == typeof(ServerRepository));
                    if (repoDescriptor != null)
                    {
                        services.Remove(repoDescriptor);
                    }
                    services.AddSingleton<ServerRepository>();

                    // Remove and re-register MachineStatusService
                    var statusDescriptor = services.FirstOrDefault(d => d.ServiceType == typeof(MachineStatusService));
                    if (statusDescriptor != null)
                    {
                        services.Remove(statusDescriptor);
                    }
                    services.AddSingleton<MachineStatusService>();
                });
            });

        _client = _factory.CreateClient();

        // Initialize database for the test
        var schema = _factory.Services.GetRequiredService<ServerSchema>();
        await schema.InitializeDatabaseAsync();
    }

    public async Task DisposeAsync()
    {
        _client?.Dispose();
        await (_factory?.DisposeAsync() ?? ValueTask.CompletedTask);
    }

    [Fact]
    public async Task IndexPage_Loads_WithoutError()
    {
        // Act
        var response = await _client!.GetAsync("/");

        // Assert
        Assert.True(response.IsSuccessStatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("StorageWatch", content);
    }

    [Fact]
    public async Task AlertsPage_Loads_WithoutError()
    {
        // Act
        var response = await _client!.GetAsync("/alerts");

        // Assert
        Assert.True(response.IsSuccessStatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("Alerts", content);
    }

    [Fact]
    public async Task SettingsPage_Loads_WithoutError()
    {
        // Act
        var response = await _client!.GetAsync("/settings");

        // Assert
        Assert.True(response.IsSuccessStatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("Settings", content);
    }

    [Fact]
    public async Task MachineDetailsPage_WithValidId_Loads()
    {
        // Arrange - First add a machine via API
        var report = TestDataFactory.CreateAgentReport("TestMachine");
        var postResponse = await _client!.PostAsJsonAsync("/api/agent/report", report);
        Assert.True(postResponse.IsSuccessStatusCode);

        // Get the machine ID
        var machinesResponse = await _client.GetAsync("/api/machines");
        var content = await machinesResponse.Content.ReadAsStringAsync();
        // Machine ID should be 1 from the first insertion
        var machineId = 1;

        // Act
        var response = await _client.GetAsync($"/machines/{machineId}");

        // Assert
        Assert.True(response.IsSuccessStatusCode);
    }

    [Fact]
    public async Task NavigationLinks_ArePresent_OnIndexPage()
    {
        // Act
        var response = await _client!.GetAsync("/");
        var content = await response.Content.ReadAsStringAsync();

        // Assert
        Assert.Contains("/alerts", content);
        Assert.Contains("/settings", content);
        Assert.Contains("Dashboard", content);
    }
}
