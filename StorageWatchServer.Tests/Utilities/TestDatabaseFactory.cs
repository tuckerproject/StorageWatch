using Microsoft.Data.Sqlite;
using StorageWatchServer.Server.Data;
using StorageWatchServer.Server.Services;

namespace StorageWatchServer.Tests.Utilities;

/// <summary>
/// Factory for creating test databases using in-memory SQLite with shared cache.
/// Each factory gets its own isolated in-memory database.
/// </summary>
public class TestDatabaseFactory : IAsyncDisposable
{
    private readonly SqliteConnection _connection;
    private readonly ServerOptions _options;
    private readonly ServerSchema _schema;
    private readonly string _databaseId;

    private TestDatabaseFactory(SqliteConnection connection, ServerOptions options, ServerSchema schema, string databaseId)
    {
        _connection = connection;
        _options = options;
        _schema = schema;
        _databaseId = databaseId;
    }

    public static async Task<TestDatabaseFactory> CreateAsync()
    {
        // Create a unique in-memory database for this test
        // Using a unique database ID ensures test isolation
        var databaseId = Guid.NewGuid().ToString("N")[..8]; // Use first 8 chars of GUID
        var connectionString = $"Data Source=file:memdb_{databaseId}?mode=memory&cache=shared";
        
        var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync();

        var options = new ServerOptions
        {
            ListenUrl = "http://localhost:5001",
            DatabasePath = $"file:memdb_{databaseId}?mode=memory&cache=shared",
            OnlineTimeoutMinutes = 5
        };

        var schema = new ServerSchema(options);
        await schema.InitializeDatabaseAsync();

        return new TestDatabaseFactory(connection, options, schema, databaseId);
    }

    public ServerRepository CreateRepository()
    {
        return new ServerRepository(_options);
    }

    public ServerOptions GetOptions() => _options;

    public async ValueTask DisposeAsync()
    {
        await _connection.DisposeAsync();
    }
}
