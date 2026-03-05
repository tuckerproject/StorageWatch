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
                        DatabasePath = $"file:memdb_{_testDatabaseId}?mode=memory&cache=shared",
                        OnlineTimeoutMinutes = 5
                    };

                    services.AddSingleton(serverOptions);
                });
            });

        _client = _factory.CreateClient();
        await Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        _client?.Dispose();
        _factory?.Dispose();
        await Task.CompletedTask;
    }

    [Fact(Skip = "Old API endpoints tests - to be rewritten for new architecture")]
    public async Task PostAgentReport_ValidRequest_ReturnsOk()
    {
        await Task.CompletedTask;
    }
}
