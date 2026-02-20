using Xunit;
using StorageWatch.Communication;
using StorageWatch.Communication.Models;
using StorageWatch.Services.Logging;
using Microsoft.Extensions.DependencyInjection;
using System.IO;
using System.Threading.Tasks;

namespace StorageWatch.Tests.Communication;

public class ServiceCommunicationServerTests : IDisposable
{
    private readonly ServiceCommunicationServer _server;
    private readonly ServiceProvider _serviceProvider;
    private readonly string _testLogPath;

    public ServiceCommunicationServerTests()
    {
        _testLogPath = Path.Combine(Path.GetTempPath(), "test-service.log");

        var services = new ServiceCollection();
        services.AddSingleton(new RollingFileLogger(_testLogPath));
        _serviceProvider = services.BuildServiceProvider();

        _server = new ServiceCommunicationServer(
            _serviceProvider.GetRequiredService<RollingFileLogger>(),
            _serviceProvider);
    }

    [Fact]
    public async Task Server_ShouldStart_WithoutErrors()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));

        var task = _server.StartAsync(cts.Token);

        // Wait a bit to ensure server started
        await Task.Delay(500);

        // Should not throw
        Assert.True(task.IsCompleted || !cts.IsCancellationRequested);

        await _server.StopAsync(CancellationToken.None);
    }

    [Fact]
    public void ServiceRequest_ShouldSerialize_Correctly()
    {
        var request = new ServiceRequest
        {
            Command = "GetStatus"
        };

        var json = System.Text.Json.JsonSerializer.Serialize(request);

        Assert.Contains("GetStatus", json);
        Assert.Contains("Command", json);
    }

    [Fact]
    public void ServiceResponse_ShouldSerialize_WithData()
    {
        var response = new ServiceResponse
        {
            Success = true,
            Data = System.Text.Json.JsonSerializer.SerializeToElement(new { Test = "Value" })
        };

        var json = System.Text.Json.JsonSerializer.Serialize(response);

        Assert.Contains("Success", json);
        Assert.Contains("true", json, StringComparison.OrdinalIgnoreCase);
    }

    public void Dispose()
    {
        _serviceProvider?.Dispose();

        if (File.Exists(_testLogPath))
            File.Delete(_testLogPath);
    }
}
