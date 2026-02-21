using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using StorageWatchServer.Server.Api;
using StorageWatchServer.Server.Data;
using StorageWatchServer.Server.Reporting.Data;
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
                        AgentReportDatabasePath = $"file:memdb_api_agent_{_testDatabaseId}?mode=memory&cache=shared",
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

                    // Remove and re-register AgentReportSchema
                    var reportSchemaDescriptor = services.FirstOrDefault(d => d.ServiceType == typeof(AgentReportSchema));
                    if (reportSchemaDescriptor != null)
                    {
                        services.Remove(reportSchemaDescriptor);
                    }
                    services.AddSingleton<AgentReportSchema>();

                    // Remove and re-register ServerRepository
                    var repoDescriptor = services.FirstOrDefault(d => d.ServiceType == typeof(ServerRepository));
                    if (repoDescriptor != null)
                    {
                        services.Remove(repoDescriptor);
                    }
                    services.AddSingleton<ServerRepository>();

                    // Remove and re-register AgentReportRepository
                    var reportRepoDescriptor = services.FirstOrDefault(d => d.ServiceType == typeof(IAgentReportRepository));
                    if (reportRepoDescriptor != null)
                    {
                        services.Remove(reportRepoDescriptor);
                    }
                    services.AddSingleton<IAgentReportRepository, AgentReportRepository>();

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

        // Initialize databases for the test
        var schema = _factory.Services.GetRequiredService<ServerSchema>();
        await schema.InitializeDatabaseAsync();

        var reportSchema = _factory.Services.GetRequiredService<AgentReportSchema>();
        await reportSchema.InitializeDatabaseAsync();
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
        var report = TestDataFactory.CreateAgentReport("TestAgent");

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
            AgentId = "TestAgent",
            TimestampUtc = DateTime.UtcNow,
            Drives = new List<DriveReportDto>()
        };

        // Act
        var response = await _client!.PostAsJsonAsync("/api/agent/report", report);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PostAgentReport_WithEmptyAgentId_ReturnsBadRequest()
    {
        // Arrange
        var report = new AgentReportRequest
        {
            AgentId = string.Empty,
            TimestampUtc = DateTime.UtcNow,
            Drives = new List<DriveReportDto>
            {
                new DriveReportDto
                {
                    DriveLetter = "C:",
                    TotalSpaceGb = 500,
                    FreeSpaceGb = 250,
                    UsedPercent = 50
                }
            }
        };

        // Act
        var response = await _client!.PostAsJsonAsync("/api/agent/report", report);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PostAgentReport_PersistsReportData()
    {
        // Arrange
        var report = TestDataFactory.CreateAgentReport("TestAgent", alertCount: 2);
        var repository = _factory!.Services.GetRequiredService<IAgentReportRepository>();

        // Act
        var response = await _client!.PostAsJsonAsync("/api/agent/report", report);
        var reports = await repository.GetRecentReportsAsync(5);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotEmpty(reports);
        var saved = reports.FirstOrDefault(r => r.AgentId == report.AgentId);
        Assert.NotNull(saved);
        Assert.Equal(report.Drives.Count, saved!.Drives.Count);
        Assert.Equal(report.Alerts.Count, saved.Alerts.Count);
    }

    [Fact]
    public async Task GetMachines_ReturnsListOfMachines()
    {
        // Arrange
        var repository = _factory!.Services.GetRequiredService<ServerRepository>();
        var now = DateTime.UtcNow;
        await repository.UpsertMachineAsync("TestMachine", now);

        // Act
        var response = await _client!.GetAsync("/api/machines");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.NotNull(content);
    }

    [Fact]
    public async Task GetMachineById_WithValidId_ReturnsMachine()
    {
        // Arrange
        var repository = _factory!.Services.GetRequiredService<ServerRepository>();
        var now = DateTime.UtcNow;
        var machineId = await repository.UpsertMachineAsync("TestMachine", now);

        // Act
        var response = await _client!.GetAsync($"/api/machines/{machineId}");

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
        var repository = _factory!.Services.GetRequiredService<ServerRepository>();
        var now = DateTime.UtcNow;
        var machineId = await repository.UpsertMachineAsync("TestMachine", now);
        await repository.InsertDiskHistoryAsync(machineId, "C:", TestDataFactory.CreateHistoryPoint(now, 75));

        // Act
        var response = await _client!.GetAsync($"/api/machines/{machineId}/history?drive=C:&range=7d");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetMachineHistory_WithoutDrive_ReturnsBadRequest()
    {
        // Arrange
        var repository = _factory!.Services.GetRequiredService<ServerRepository>();
        var now = DateTime.UtcNow;
        var machineId = await repository.UpsertMachineAsync("TestMachine", now);

        // Act
        var response = await _client!.GetAsync($"/api/machines/{machineId}/history?range=7d");

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
    }

    [Fact]
    public async Task GetRecentReports_ReturnsDashboardReportSummary()
    {
        // Arrange
        var report = TestDataFactory.CreateAgentReport("DashboardAgent");
        var postResponse = await _client!.PostAsJsonAsync("/api/agent/report", report);
        Assert.Equal(HttpStatusCode.OK, postResponse.StatusCode);

        // Act
        var response = await _client.GetAsync("/api/dashboard/reports/recent?count=1");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var results = JsonSerializer.Deserialize<List<DashboardReportResponseDto>>(content, options);
        Assert.NotNull(results);
        var item = results!.FirstOrDefault();
        Assert.NotNull(item);
        Assert.Equal("DashboardAgent", item!.AgentId);
        Assert.NotEmpty(item.Drives);
    }
}

public sealed class DashboardReportResponseDto
{
    public string AgentId { get; set; } = string.Empty;

    public DateTime TimestampUtc { get; set; }

    public List<DashboardDriveSummaryDto> Drives { get; set; } = new();

    public List<DashboardAlertSummaryDto> Alerts { get; set; } = new();
}

public sealed class DashboardDriveSummaryDto
{
    public string DriveLetter { get; set; } = string.Empty;

    public double UsedPercent { get; set; }
}

public sealed class DashboardAlertSummaryDto
{
    public string DriveLetter { get; set; } = string.Empty;

    public string Level { get; set; } = string.Empty;

    public string Message { get; set; } = string.Empty;
}
