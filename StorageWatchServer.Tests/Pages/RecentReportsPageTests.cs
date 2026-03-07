using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using StorageWatchServer.Dashboard;
using StorageWatchServer.Server.Data;
using StorageWatchServer.Server.Models;
using StorageWatchServer.Server.Reporting;
using StorageWatchServer.Server.Services;
using Xunit;

namespace StorageWatchServer.Tests.Pages;

/// <summary>
/// Tests for the Recent Reports page (/reports).
/// Validates grouping by machineName and ordering by timestamp.
/// </summary>
public class RecentReportsPageTests : IAsyncLifetime
{
    private WebApplicationFactory<Program>? _factory;
    private readonly string _testDatabaseId = Guid.NewGuid().ToString("N")[..8];
    private ServerSchema? _schema;
    private RawRowIngestionService? _ingestionService;

    public async Task InitializeAsync()
    {
        // Set test environment flag before building the factory
        AppContext.SetData("IsTestEnvironment", true);
        
        _factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                // Exclude configuration file loading in tests
                builder.ConfigureAppConfiguration((context, config) =>
                {
                    config.Sources.Clear();
                    config.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        { "Server:ListenUrl", "http://localhost:5001" },
                        { "Server:DatabasePath", $"file:memdb_reports_{_testDatabaseId}?mode=memory&cache=shared" },
                        { "Server:OnlineTimeoutMinutes", "5" }
                    });
                });

                builder.ConfigureServices(services =>
                {
                    var serverOptions = new ServerOptions
                    {
                        ListenUrl = "http://localhost:5001",
                        DatabasePath = $"file:memdb_reports_{_testDatabaseId}?mode=memory&cache=shared",
                        OnlineTimeoutMinutes = 5
                    };

                    var descriptor = services.FirstOrDefault(d => d.ServiceType == typeof(ServerOptions));
                    if (descriptor != null)
                    {
                        services.Remove(descriptor);
                    }
                    services.AddSingleton(serverOptions);

                    var schemaDescriptor = services.FirstOrDefault(d => d.ServiceType == typeof(ServerSchema));
                    if (schemaDescriptor != null)
                    {
                        services.Remove(schemaDescriptor);
                    }
                    services.AddSingleton<ServerSchema>();

                    var ingestionDescriptor = services.FirstOrDefault(d => d.ServiceType == typeof(RawRowIngestionService));
                    if (ingestionDescriptor != null)
                    {
                        services.Remove(ingestionDescriptor);
                    }
                    services.AddSingleton<RawRowIngestionService>();
                });
            });

        _schema = _factory.Services.GetRequiredService<ServerSchema>();
        _ingestionService = _factory.Services.GetRequiredService<RawRowIngestionService>();

        await _schema.InitializeDatabaseAsync();
    }

    public async Task DisposeAsync()
    {
        _factory?.Dispose();
        await Task.CompletedTask;
    }

    [Fact]
    public async Task ReportsPage_GroupsByMachineName()
    {
        // Arrange - insert rows from multiple machines
        var now = DateTime.UtcNow;

        await _ingestionService!.IngestRawRowsAsync("Machine1", new List<RawDriveRow>
        {
            new()
            {
                MachineName = "Machine1",
                DriveLetter = "C:",
                TotalSpaceGb = 500,
                UsedSpaceGb = 250,
                FreeSpaceGb = 250,
                PercentFree = 50,
                Timestamp = now
            }
        });

        await _ingestionService.IngestRawRowsAsync("Machine2", new List<RawDriveRow>
        {
            new()
            {
                MachineName = "Machine2",
                DriveLetter = "D:",
                TotalSpaceGb = 1000,
                UsedSpaceGb = 500,
                FreeSpaceGb = 500,
                PercentFree = 50,
                Timestamp = now
            }
        });

        // Act - directly test the model, not the HTTP endpoint
        var model = new ReportsModel(
            _factory!.Services.GetRequiredService<ServerOptions>(),
            _factory.Services.GetRequiredService<ILogger<ReportsModel>>());

        await model.OnGetAsync();

        // Assert
        Assert.NotEmpty(model.RecentReportsByMachine);
        Assert.True(model.RecentReportsByMachine.Any(m => m.MachineName == "Machine1"));
        Assert.True(model.RecentReportsByMachine.Any(m => m.MachineName == "Machine2"));
    }

    [Fact]
    public async Task ReportsPage_ShowsLatestTimestampPerMachine()
    {
        // Arrange - insert multiple reports from same machine with different timestamps
        var baseTime = DateTime.UtcNow;
        var oldTime = baseTime.AddHours(-2);
        var latestTime = baseTime.AddHours(-1);

        await _ingestionService!.IngestRawRowsAsync("Machine1", new List<RawDriveRow>
        {
            new()
            {
                MachineName = "Machine1",
                DriveLetter = "C:",
                TotalSpaceGb = 500,
                UsedSpaceGb = 200,
                FreeSpaceGb = 300,
                PercentFree = 60,
                Timestamp = oldTime
            }
        });

        await _ingestionService.IngestRawRowsAsync("Machine1", new List<RawDriveRow>
        {
            new()
            {
                MachineName = "Machine1",
                DriveLetter = "C:",
                TotalSpaceGb = 500,
                UsedSpaceGb = 250,
                FreeSpaceGb = 250,
                PercentFree = 50,
                Timestamp = latestTime
            }
        });

        // Act
        var model = new ReportsModel(
            _factory!.Services.GetRequiredService<ServerOptions>(),
            _factory.Services.GetRequiredService<ILogger<ReportsModel>>());

        await model.OnGetAsync();

        // Assert
        Assert.NotEmpty(model.RecentReportsByMachine);
        var machineGroup = model.RecentReportsByMachine.FirstOrDefault(m => m.MachineName == "Machine1");
        Assert.NotNull(machineGroup);
        Assert.NotEmpty(machineGroup!.Rows);
        // Verify the latest timestamp is shown
        Assert.True(machineGroup.LatestTimestamp > oldTime);
    }

    [Fact]
    public async Task ReportsPage_RetainsAllFieldsFromLatestReport()
    {
        // Arrange
        var now = DateTime.UtcNow;

        await _ingestionService!.IngestRawRowsAsync("Machine1", new List<RawDriveRow>
        {
            new()
            {
                MachineName = "Machine1",
                DriveLetter = "C:",
                TotalSpaceGb = 500.5,
                UsedSpaceGb = 250.25,
                FreeSpaceGb = 250.25,
                PercentFree = 50.05,
                Timestamp = now
            },
            new()
            {
                MachineName = "Machine1",
                DriveLetter = "D:",
                TotalSpaceGb = 1000.75,
                UsedSpaceGb = 600.5,
                FreeSpaceGb = 400.25,
                PercentFree = 40.025,
                Timestamp = now
            }
        });

        // Act
        var model = new ReportsModel(
            _factory!.Services.GetRequiredService<ServerOptions>(),
            _factory.Services.GetRequiredService<ILogger<ReportsModel>>());

        await model.OnGetAsync();

        // Assert
        Assert.NotEmpty(model.RecentReportsByMachine);
        var machineGroup = model.RecentReportsByMachine.FirstOrDefault(m => m.MachineName == "Machine1");
        Assert.NotNull(machineGroup);
        Assert.Equal(2, machineGroup!.Rows.Count);

        // Verify C: drive
        var cDrive = machineGroup.Rows.FirstOrDefault(r => r.DriveLetter == "C:");
        Assert.NotNull(cDrive);
        Assert.Equal(500.5, cDrive!.TotalSpaceGb);
        Assert.Equal(250.25, cDrive.UsedSpaceGb);
        Assert.Equal(250.25, cDrive.FreeSpaceGb);
        Assert.Equal(50.05, cDrive.PercentFree);

        // Verify D: drive
        var dDrive = machineGroup.Rows.FirstOrDefault(r => r.DriveLetter == "D:");
        Assert.NotNull(dDrive);
        Assert.Equal(1000.75, dDrive!.TotalSpaceGb);
        Assert.Equal(600.5, dDrive.UsedSpaceGb);
        Assert.Equal(400.25, dDrive.FreeSpaceGb);
        Assert.Equal(40.025, dDrive.PercentFree);
    }

    [Fact]
    public async Task ReportsPage_OrdersDrivesByLetter()
    {
        // Arrange
        var now = DateTime.UtcNow;

        await _ingestionService!.IngestRawRowsAsync("Machine1", new List<RawDriveRow>
        {
            new()
            {
                MachineName = "Machine1",
                DriveLetter = "D:",
                TotalSpaceGb = 1000,
                UsedSpaceGb = 500,
                FreeSpaceGb = 500,
                PercentFree = 50,
                Timestamp = now
            },
            new()
            {
                MachineName = "Machine1",
                DriveLetter = "C:",
                TotalSpaceGb = 500,
                UsedSpaceGb = 250,
                FreeSpaceGb = 250,
                PercentFree = 50,
                Timestamp = now
            },
            new()
            {
                MachineName = "Machine1",
                DriveLetter = "E:",
                TotalSpaceGb = 2000,
                UsedSpaceGb = 1000,
                FreeSpaceGb = 1000,
                PercentFree = 50,
                Timestamp = now
            }
        });

        // Act
        var model = new ReportsModel(
            _factory!.Services.GetRequiredService<ServerOptions>(),
            _factory.Services.GetRequiredService<ILogger<ReportsModel>>());

        await model.OnGetAsync();

        // Assert
        var machineGroup = model.RecentReportsByMachine.FirstOrDefault(m => m.MachineName == "Machine1");
        Assert.NotNull(machineGroup);
        Assert.Equal(3, machineGroup!.Rows.Count);

        // Verify drives are ordered by DriveLetter
        Assert.Equal("C:", machineGroup.Rows[0].DriveLetter);
        Assert.Equal("D:", machineGroup.Rows[1].DriveLetter);
        Assert.Equal("E:", machineGroup.Rows[2].DriveLetter);
    }

    [Fact]
    public async Task ReportsPage_HandlesEmptyDatabase()
    {
        // Arrange - no data inserted

        // Act
        var model = new ReportsModel(
            _factory!.Services.GetRequiredService<ServerOptions>(),
            _factory.Services.GetRequiredService<ILogger<ReportsModel>>());

        await model.OnGetAsync();

        // Assert
        Assert.Empty(model.RecentReportsByMachine);
    }
}
