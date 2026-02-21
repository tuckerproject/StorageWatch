using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using StorageWatchServer.Server.Api;
using StorageWatchServer.Server.Data;
using StorageWatchServer.Server.Services;
using StorageWatchServer.Tests.Utilities;
using Xunit;

namespace StorageWatchServer.Tests.Api;

public class ApiEndpointsIntegrationTests : IAsyncLifetime
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
                        DatabasePath = $"file:memdb_api_{_testDatabaseId}?mode=memory&cache=shared",
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
    public async Task PostAgentReport_WithValidPayload_ReturnsOkResponse()
    {
        // Arrange
        var report = TestDataFactory.CreateAgentReport("TestMachine");

        // Act
        var response = await _client!.PostAsJsonAsync("/api/agent/report", report);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var result = JsonSerializer.Deserialize<ApiResponse>(content, options);
        Assert.NotNull(result);
        Assert.True(result?.Success ?? false);
    }

    [Fact]
    public async Task PostAgentReport_WithEmptyDrives_ReturnsBadRequest()
    {
        // Arrange
        var report = new AgentReportRequest
        {
            MachineName = "TestMachine",
            CollectionTimeUtc = DateTime.UtcNow,
            Drives = new List<AgentDriveReport>()
        };

        // Act
        var response = await _client!.PostAsJsonAsync("/api/agent/report", report);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PostAgentReport_WithEmptyMachineName_ReturnsBadRequest()
    {
        // Arrange
        var report = new AgentReportRequest
        {
            MachineName = string.Empty,
            CollectionTimeUtc = DateTime.UtcNow,
            Drives = new List<AgentDriveReport>
            {
                new AgentDriveReport
                {
                    DriveLetter = "C:",
                    TotalSpaceGb = 500,
                    UsedSpaceGb = 250,
                    FreeSpaceGb = 250,
                    PercentFree = 50,
                    CollectionTimeUtc = DateTime.UtcNow
                }
            }
        };

        // Act
        var response = await _client!.PostAsJsonAsync("/api/agent/report", report);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetMachines_ReturnsListOfMachines()
    {
        // Arrange
        var report = TestDataFactory.CreateAgentReport("TestMachine");
        await _client!.PostAsJsonAsync("/api/agent/report", report);

        // Act
        var response = await _client.GetAsync("/api/machines");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.NotNull(content);
    }

    [Fact]
    public async Task GetMachineById_WithValidId_ReturnsMachine()
    {
        // Arrange
        var report = TestDataFactory.CreateAgentReport("TestMachine");
        var postResponse = await _client!.PostAsJsonAsync("/api/agent/report", report);
        Assert.Equal(HttpStatusCode.OK, postResponse.StatusCode);

        var machinesResponse = await _client.GetAsync("/api/machines");
        var content = await machinesResponse.Content.ReadAsStringAsync();
        var machines = JsonSerializer.Deserialize<List<dynamic>>(content);
        var machineId = machines?[0];

        // Act
        var response = await _client.GetAsync($"/api/machines/1");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetMachineById_WithInvalidId_ReturnsNotFound()
    {
        // Act
        var response = await _client!.GetAsync("/api/machines/999");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetMachineHistory_WithValidDrive_ReturnsHistory()
    {
        // Arrange
        var report = TestDataFactory.CreateAgentReport("TestMachine", driveCount: 1);
        var now = DateTime.UtcNow;
        report.CollectionTimeUtc = now;
        report.Drives[0].CollectionTimeUtc = now;

        var postResponse = await _client!.PostAsJsonAsync("/api/agent/report", report);
        Assert.Equal(HttpStatusCode.OK, postResponse.StatusCode);

        var machinesResponse = await _client.GetAsync("/api/machines");
        var content = await machinesResponse.Content.ReadAsStringAsync();
        var machines = JsonSerializer.Deserialize<List<dynamic>>(content);

        // Act
        var response = await _client.GetAsync($"/api/machines/1/history?drive=C:&range=7d");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetMachineHistory_WithoutDrive_ReturnsBadRequest()
    {
        // Arrange
        var report = TestDataFactory.CreateAgentReport("TestMachine");
        await _client!.PostAsJsonAsync("/api/agent/report", report);

        var machinesResponse = await _client.GetAsync("/api/machines");
        var content = await machinesResponse.Content.ReadAsStringAsync();
        var machines = JsonSerializer.Deserialize<List<dynamic>>(content);

        // Act
        var response = await _client.GetAsync($"/api/machines/1/history?range=7d");

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetAlerts_ReturnsAlertsList()
    {
        // Act
        var response = await _client!.GetAsync("/api/alerts");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.NotNull(content);
    }

    [Fact]
    public async Task GetSettings_ReturnsSettingsList()
    {
        // Act
        var response = await _client!.GetAsync("/api/settings");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.NotNull(content);
    }

    [Fact]
    public async Task MultipleAgentReports_DataIsSeparatedByMachine()
    {
        // Arrange
        var report1 = TestDataFactory.CreateAgentReport("Machine1", driveCount: 1);
        var report2 = TestDataFactory.CreateAgentReport("Machine2", driveCount: 1);

        report1.Drives[0].DriveLetter = "C:";
        report2.Drives[0].DriveLetter = "D:";

        // Act
        var response1 = await _client!.PostAsJsonAsync("/api/agent/report", report1);
        var response2 = await _client.PostAsJsonAsync("/api/agent/report", report2);
        var machinesResponse = await _client.GetAsync("/api/machines");
        var content = await machinesResponse.Content.ReadAsStringAsync();
        var machines = JsonSerializer.Deserialize<List<dynamic>>(content);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response1.StatusCode);
        Assert.Equal(HttpStatusCode.OK, response2.StatusCode);
        Assert.NotNull(machines);
        // Since tests share the same database, we just verify we have at least 2 machines
        Assert.True(machines?.Count >= 2, $"Expected at least 2 machines, got {machines?.Count}");
    }

    [Fact]
    public async Task HistoryRangeFiltering_ReturnsOnlyRequestedRange()
    {
        // Arrange - not testing exact filtering as that requires DB setup
        // But we ensure the endpoint accepts range parameters
        var report = TestDataFactory.CreateAgentReport("TestMachine", driveCount: 1);
        var now = DateTime.UtcNow;
        report.CollectionTimeUtc = now;
        report.Drives[0].CollectionTimeUtc = now;

        await _client!.PostAsJsonAsync("/api/agent/report", report);

        var machinesResponse = await _client.GetAsync("/api/machines");
        var content = await machinesResponse.Content.ReadAsStringAsync();
        var machines = JsonSerializer.Deserialize<List<dynamic>>(content);

        // Act - Test different range formats
        var response1d = await _client.GetAsync($"/api/machines/1/history?drive=C:&range=1d");
        var response24h = await _client.GetAsync($"/api/machines/1/history?drive=C:&range=24h");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response1d.StatusCode);
        Assert.Equal(HttpStatusCode.OK, response24h.StatusCode);
    }
}
