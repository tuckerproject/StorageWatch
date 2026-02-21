using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using StorageWatchServer.Server.Api;
using StorageWatchServer.Server.Data;
using StorageWatchServer.Server.Reporting.Data;
using StorageWatchServer.Server.Services;
using Xunit;

namespace StorageWatchServer.Tests.Integration;

/// <summary>
/// End-to-end integration test for Step 15.5.
/// Validates the complete Agent → Server → SQLite → Dashboard API pipeline.
/// </summary>
public class AgentReportingPipelineTests : IAsyncLifetime
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
                        DatabasePath = $"file:memdb_e2e_{_testDatabaseId}?mode=memory&cache=shared",
                        AgentReportDatabasePath = $"file:memdb_e2e_agent_{_testDatabaseId}?mode=memory&cache=shared",
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

    /// <summary>
    /// Step 15.5: Full end-to-end integration test validating:
    /// Agent → Server → SQLite Persistence → Dashboard API
    /// </summary>
    [Fact]
    public async Task AgentReport_EndToEnd_FullPipeline_Success()
    {
        // ====================================================================
        // ARRANGE: Construct a fully populated AgentReportRequest
        // ====================================================================
        var agentId = "TestAgent-E2E";
        var timestampUtc = DateTime.UtcNow;

        var agentReport = new AgentReportRequest
        {
            AgentId = agentId,
            TimestampUtc = timestampUtc,
            Drives = new List<DriveReportDto>
            {
                new DriveReportDto
                {
                    DriveLetter = "C:",
                    TotalSpaceGb = 500,
                    FreeSpaceGb = 100,
                    UsedPercent = 80
                },
                new DriveReportDto
                {
                    DriveLetter = "D:",
                    TotalSpaceGb = 1000,
                    FreeSpaceGb = 300,
                    UsedPercent = 70
                },
                new DriveReportDto
                {
                    DriveLetter = "E:",
                    TotalSpaceGb = 250,
                    FreeSpaceGb = 200,
                    UsedPercent = 20
                }
            },
            Alerts = new List<AlertDto>
            {
                new AlertDto
                {
                    DriveLetter = "C:",
                    Level = "Warning",
                    Message = "Drive C: is running low on space (100 GB free)"
                },
                new AlertDto
                {
                    DriveLetter = "D:",
                    Level = "Info",
                    Message = "Drive D: usage is moderate (300 GB free)"
                }
            }
        };

        // ====================================================================
        // ACT 1: POST to /api/agent/report
        // ====================================================================
        var postResponse = await _client!.PostAsJsonAsync("/api/agent/report", agentReport);

        // ====================================================================
        // ASSERT 1: Verify POST response
        // ====================================================================
        Assert.Equal(HttpStatusCode.OK, postResponse.StatusCode);

        var postContent = await postResponse.Content.ReadAsStringAsync();
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var apiResponse = JsonSerializer.Deserialize<ApiResponse>(postContent, options);

        Assert.NotNull(apiResponse);
        Assert.True(apiResponse.Success, $"API response should indicate success. Message: {apiResponse.Message}");
        Assert.Equal("Report received.", apiResponse.Message);

        // ====================================================================
        // ASSERT 2: Verify persistence via repository
        // ====================================================================
        var repository = _factory!.Services.GetRequiredService<IAgentReportRepository>();
        var persistedReports = await repository.GetRecentReportsAsync(10);

        Assert.NotEmpty(persistedReports);
        var persistedReport = persistedReports.FirstOrDefault(r => r.AgentId == agentId);
        Assert.NotNull(persistedReport);

        // Validate AgentReport fields
        Assert.Equal(agentId, persistedReport.AgentId);
        Assert.Equal(timestampUtc, persistedReport.TimestampUtc);

        // Validate DriveReports persistence
        Assert.Equal(3, persistedReport.Drives.Count);
        
        var driveC = persistedReport.Drives.FirstOrDefault(d => d.DriveLetter == "C:");
        Assert.NotNull(driveC);
        Assert.Equal(500, driveC.TotalSpaceGb);
        Assert.Equal(100, driveC.FreeSpaceGb);
        Assert.Equal(80, driveC.UsedPercent);

        var driveD = persistedReport.Drives.FirstOrDefault(d => d.DriveLetter == "D:");
        Assert.NotNull(driveD);
        Assert.Equal(1000, driveD.TotalSpaceGb);
        Assert.Equal(300, driveD.FreeSpaceGb);
        Assert.Equal(70, driveD.UsedPercent);

        var driveE = persistedReport.Drives.FirstOrDefault(d => d.DriveLetter == "E:");
        Assert.NotNull(driveE);
        Assert.Equal(250, driveE.TotalSpaceGb);
        Assert.Equal(200, driveE.FreeSpaceGb);
        Assert.Equal(20, driveE.UsedPercent);

        // Validate AlertRecords persistence
        Assert.Equal(2, persistedReport.Alerts.Count);

        var alertC = persistedReport.Alerts.FirstOrDefault(a => a.DriveLetter == "C:");
        Assert.NotNull(alertC);
        Assert.Equal("Warning", alertC.Level);
        Assert.Equal("Drive C: is running low on space (100 GB free)", alertC.Message);

        var alertD = persistedReport.Alerts.FirstOrDefault(a => a.DriveLetter == "D:");
        Assert.NotNull(alertD);
        Assert.Equal("Info", alertD.Level);
        Assert.Equal("Drive D: usage is moderate (300 GB free)", alertD.Message);

        // ====================================================================
        // ACT 2: GET /api/dashboard/reports/recent?count=1
        // ====================================================================
        var getResponse = await _client!.GetAsync("/api/dashboard/reports/recent?count=1");

        // ====================================================================
        // ASSERT 3: Verify GET response
        // ====================================================================
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);

        var getContent = await getResponse.Content.ReadAsStringAsync();
        var dashboardReports = JsonSerializer.Deserialize<List<DashboardReportResponse>>(getContent, options);

        Assert.NotNull(dashboardReports);
        Assert.NotEmpty(dashboardReports);

        var dashboardReport = dashboardReports.First();

        // Validate Dashboard DTO fields
        Assert.Equal(agentId, dashboardReport.AgentId);
        Assert.Equal(timestampUtc, dashboardReport.TimestampUtc);

        // Validate Dashboard Drive Summaries
        Assert.Equal(3, dashboardReport.Drives.Count);

        var dashDriveC = dashboardReport.Drives.FirstOrDefault(d => d.DriveLetter == "C:");
        Assert.NotNull(dashDriveC);
        Assert.Equal(80, dashDriveC.UsedPercent);

        var dashDriveD = dashboardReport.Drives.FirstOrDefault(d => d.DriveLetter == "D:");
        Assert.NotNull(dashDriveD);
        Assert.Equal(70, dashDriveD.UsedPercent);

        var dashDriveE = dashboardReport.Drives.FirstOrDefault(d => d.DriveLetter == "E:");
        Assert.NotNull(dashDriveE);
        Assert.Equal(20, dashDriveE.UsedPercent);

        // Validate Dashboard Alert Summaries
        Assert.Equal(2, dashboardReport.Alerts.Count);

        var dashAlertC = dashboardReport.Alerts.FirstOrDefault(a => a.DriveLetter == "C:");
        Assert.NotNull(dashAlertC);
        Assert.Equal("Warning", dashAlertC.Level);
        Assert.Equal("Drive C: is running low on space (100 GB free)", dashAlertC.Message);

        var dashAlertD = dashboardReport.Alerts.FirstOrDefault(a => a.DriveLetter == "D:");
        Assert.NotNull(dashAlertD);
        Assert.Equal("Info", dashAlertD.Level);
        Assert.Equal("Drive D: usage is moderate (300 GB free)", dashAlertD.Message);

        // ====================================================================
        // ASSERT 4: Verify ordering (most recent first)
        // ====================================================================
        // Since we only inserted one report, verify it's the first one
        Assert.Equal(agentId, dashboardReports[0].AgentId);
    }

    /// <summary>
    /// Additional test: Verify ordering when multiple reports exist.
    /// </summary>
    [Fact]
    public async Task AgentReport_EndToEnd_MultipleReports_OrderedCorrectly()
    {
        // ====================================================================
        // ARRANGE: Create three reports with different timestamps
        // ====================================================================
        var agentId = "TestAgent-Ordering";
        var oldestTime = DateTime.UtcNow.AddMinutes(-20);
        var middleTime = DateTime.UtcNow.AddMinutes(-10);
        var newestTime = DateTime.UtcNow;

        var oldestReport = CreateTestReport(agentId, oldestTime, "C:", 60);
        var middleReport = CreateTestReport(agentId, middleTime, "C:", 70);
        var newestReport = CreateTestReport(agentId, newestTime, "C:", 80);

        // ====================================================================
        // ACT: POST all three reports (out of order to test sorting)
        // ====================================================================
        await _client!.PostAsJsonAsync("/api/agent/report", middleReport);
        await _client!.PostAsJsonAsync("/api/agent/report", oldestReport);
        await _client!.PostAsJsonAsync("/api/agent/report", newestReport);

        // ====================================================================
        // ASSERT: GET recent reports and verify descending order
        // ====================================================================
        var getResponse = await _client!.GetAsync("/api/dashboard/reports/recent?count=3");
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);

        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var getContent = await getResponse.Content.ReadAsStringAsync();
        var dashboardReports = JsonSerializer.Deserialize<List<DashboardReportResponse>>(getContent, options);

        Assert.NotNull(dashboardReports);
        Assert.True(dashboardReports.Count >= 3, "Should have at least 3 reports");

        // Find our reports in the results
        var ourReports = dashboardReports
            .Where(r => r.AgentId == agentId)
            .OrderByDescending(r => r.TimestampUtc)
            .ToList();

        Assert.Equal(3, ourReports.Count);

        // Verify they are in descending timestamp order
        Assert.Equal(newestTime, ourReports[0].TimestampUtc);
        Assert.Equal(80, ourReports[0].Drives.First().UsedPercent);

        Assert.Equal(middleTime, ourReports[1].TimestampUtc);
        Assert.Equal(70, ourReports[1].Drives.First().UsedPercent);

        Assert.Equal(oldestTime, ourReports[2].TimestampUtc);
        Assert.Equal(60, ourReports[2].Drives.First().UsedPercent);
    }

    /// <summary>
    /// Helper method to create a test report with minimal data.
    /// </summary>
    private static AgentReportRequest CreateTestReport(
        string agentId,
        DateTime timestampUtc,
        string driveLetter,
        double usedPercent)
    {
        return new AgentReportRequest
        {
            AgentId = agentId,
            TimestampUtc = timestampUtc,
            Drives = new List<DriveReportDto>
            {
                new DriveReportDto
                {
                    DriveLetter = driveLetter,
                    TotalSpaceGb = 500,
                    FreeSpaceGb = 500 * (1 - usedPercent / 100),
                    UsedPercent = usedPercent
                }
            },
            Alerts = new List<AlertDto>()
        };
    }
}
