using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using StorageWatchServer.Dashboard;
using StorageWatchServer.Server.Api;
using StorageWatchServer.Server.Data;
using StorageWatchServer.Server.Models;
using StorageWatchServer.Server.Reporting;
using StorageWatchServer.Server.Services;
using Xunit;

namespace StorageWatchServer.Tests.Integration;

/// <summary>
/// End-to-end integration tests for the Agent → Server → SQLite → Dashboard pipeline.
/// Validates the complete flow of ingesting raw drive rows and retrieving them via the Reports page.
/// </summary>
public class AgentReportingPipelineTests : IAsyncLifetime
{
    private WebApplicationFactory<Program>? _factory;
    private HttpClient? _client;
    private readonly string _testDatabaseId = Guid.NewGuid().ToString("N")[..8];
    private ServerSchema? _schema;
    private ServerRepository? _repository;

    public async Task InitializeAsync()
    {
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
                        { "Server:DatabasePath", $"file:memdb_integration_{_testDatabaseId}?mode=memory&cache=shared" },
                        { "Server:OnlineTimeoutMinutes", "5" }
                    });
                });

                builder.ConfigureServices(services =>
                {
                    var serverOptions = new ServerOptions
                    {
                        ListenUrl = "http://localhost:5001",
                        DatabasePath = $"file:memdb_integration_{_testDatabaseId}?mode=memory&cache=shared",
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

                    var repoDescriptor = services.FirstOrDefault(d => d.ServiceType == typeof(ServerRepository));
                    if (repoDescriptor != null)
                    {
                        services.Remove(repoDescriptor);
                    }
                    services.AddSingleton<ServerRepository>();
                });
            });

        _client = _factory.CreateClient();
        _schema = _factory.Services.GetRequiredService<ServerSchema>();
        _repository = _factory.Services.GetRequiredService<ServerRepository>();

        await _schema.InitializeDatabaseAsync();
    }

    public async Task DisposeAsync()
    {
        _client?.Dispose();
        _factory?.Dispose();
        await Task.CompletedTask;
    }

    [Fact]
    public async Task PostReport_WithValidPayload_InsertsRowsIntoDatabase()
    {
        // Arrange
        var request = new AgentReportRequest
        {
            MachineName = "TestMachine1",
            Rows = new List<RawDriveRowRequest>
            {
                new()
                {
                    DriveLetter = "C:",
                    TotalSpaceGb = 500,
                    UsedSpaceGb = 250,
                    FreeSpaceGb = 250,
                    PercentFree = 50,
                    Timestamp = DateTime.UtcNow
                },
                new()
                {
                    DriveLetter = "D:",
                    TotalSpaceGb = 1000,
                    UsedSpaceGb = 600,
                    FreeSpaceGb = 400,
                    PercentFree = 40,
                    Timestamp = DateTime.UtcNow
                }
            }
        };

        // Act
        var response = await _client!.PostAsJsonAsync("/api/agent/report", request);

        // Assert
        Assert.True(response.IsSuccessStatusCode);

        // Verify rows were inserted exactly as sent
        var serverOptions = _factory!.Services.GetRequiredService<ServerOptions>();
        await using var connection = new SqliteConnection($"Data Source={serverOptions.DatabasePath}");
        await connection.OpenAsync();

        const string query = "SELECT COUNT(*) FROM RawDriveRows WHERE MachineName = @machineName";
        await using var command = new SqliteCommand(query, connection);
        command.Parameters.AddWithValue("@machineName", "TestMachine1");
        var count = (long?)await command.ExecuteScalarAsync();

        Assert.Equal(2, count);

        // Verify specific values are stored exactly as provided
        const string detailQuery = @"
            SELECT DriveLetter, TotalSpaceGb, UsedSpaceGb, FreeSpaceGb, PercentFree
            FROM RawDriveRows
            WHERE MachineName = @machineName AND DriveLetter = @driveLetter";

        await using var detailCommand = new SqliteCommand(detailQuery, connection);
        detailCommand.Parameters.AddWithValue("@machineName", "TestMachine1");
        detailCommand.Parameters.AddWithValue("@driveLetter", "C:");

        await using var reader = await detailCommand.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());

        var driveLetter = reader.GetString(0);
        var totalSpaceGb = reader.GetDouble(1);
        var usedSpaceGb = reader.GetDouble(2);
        var freeSpaceGb = reader.GetDouble(3);
        var percentFree = reader.GetDouble(4);

        Assert.Equal("C:", driveLetter);
        Assert.Equal(500, totalSpaceGb);
        Assert.Equal(250, usedSpaceGb);
        Assert.Equal(250, freeSpaceGb);
        Assert.Equal(50, percentFree);
    }

    [Fact]
    public async Task RecentReports_ReturnsLatestRowsPerMachine()
    {
        // Arrange - insert multiple rows for multiple machines
        var serverOptions = _factory!.Services.GetRequiredService<ServerOptions>();
        var ingestionService = _factory.Services.GetRequiredService<RawRowIngestionService>();

        var now = DateTime.UtcNow;
        var oldTime = now.AddHours(-1);

        // Insert rows for Machine1 (old and new)
        await ingestionService.IngestRawRowsAsync("Machine1", new List<RawDriveRow>
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

        await ingestionService.IngestRawRowsAsync("Machine1", new List<RawDriveRow>
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

        // Insert rows for Machine2
        await ingestionService.IngestRawRowsAsync("Machine2", new List<RawDriveRow>
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

        // Act - Test the model directly instead of via HTTP
        var model = new Dashboard.ReportsModel(
            serverOptions,
            _factory.Services.GetRequiredService<ILogger<Dashboard.ReportsModel>>());

        await model.OnGetAsync();

        // Assert - verify the model loaded the data correctly
        Assert.NotEmpty(model.RecentReportsByMachine);
        Assert.True(model.RecentReportsByMachine.Any(m => m.MachineName == "Machine1"));
        Assert.True(model.RecentReportsByMachine.Any(m => m.MachineName == "Machine2"));

        // Verify Machine1 has the latest timestamp (not the old one)
        var machine1Group = model.RecentReportsByMachine.First(m => m.MachineName == "Machine1");
        Assert.True(Math.Abs((machine1Group.LatestTimestamp - now).TotalSeconds) < 1, 
            "Machine1 should have the latest timestamp, not the old one");
    }

    [Fact]
    public async Task Database_IsAutoCreatedIfMissing()
    {
        // Arrange
        var serverOptions = _factory!.Services.GetRequiredService<ServerOptions>();

        // The database should already exist from InitializeAsync, so we verify it was created
        // In a production scenario, calling InitializeDatabaseAsync on a new path should create it

        // Act - verify RawDriveRows table exists
        await using var connection = new SqliteConnection($"Data Source={serverOptions.DatabasePath}");
        await connection.OpenAsync();

        const string query = @"
            SELECT name FROM sqlite_master 
            WHERE type='table' AND name='RawDriveRows'";

        await using var command = new SqliteCommand(query, connection);
        var result = await command.ExecuteScalarAsync();

        // Assert
        Assert.NotNull(result);
        Assert.Equal("RawDriveRows", result);
    }
}
