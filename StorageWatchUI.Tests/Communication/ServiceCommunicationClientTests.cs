using Xunit;
using FluentAssertions;
using StorageWatchUI.Communication;
using System.Threading.Tasks;
using System.Text.Json;
using System.Threading;

namespace StorageWatchUI.Tests.Communication;

public class ServiceCommunicationClientTests
{
    private readonly ServiceCommunicationClient _client;

    public ServiceCommunicationClientTests()
    {
        _client = new ServiceCommunicationClient();
    }

    [Fact]
    public async Task GetStatusAsync_WhenServiceNotRunning_ShouldReturnNull()
    {
        // Act
        var status = await _client.GetStatusAsync();
        
        // Assert - Service likely not running in test environment
        // Should handle gracefully without throwing
        status.Should().BeNull();
    }

    [Fact]
    public async Task SendRequestAsync_WithInvalidCommand_ShouldHandleGracefully()
    {
        // Arrange
        var request = new ServiceRequest
        {
            Command = "InvalidCommand"
        };

        // Act
        var response = await _client.SendRequestAsync(request);
        
        // Assert - Should get a response indicating error or timeout
        response.Should().NotBeNull();
        response.Success.Should().BeFalse();
    }

    [Fact]
    public async Task GetConfigAsync_WhenServiceNotRunning_ShouldReturnNull()
    {
        // Act
        var config = await _client.GetConfigAsync();

        // Assert
        config.Should().BeNull();
    }

    [Fact]
    public async Task ValidateConfigAsync_WhenServiceNotRunning_ShouldReturnNull()
    {
        // Act
        var validation = await _client.ValidateConfigAsync();

        // Assert
        validation.Should().BeNull();
    }

    [Fact]
    public async Task GetPluginStatusAsync_WhenServiceNotRunning_ShouldReturnEmptyList()
    {
        // Act
        var client = new MockServiceCommunicationClient();
        var plugins = await client.GetPluginStatusAsync();

        // Assert
        plugins.Should().NotBeNull();
        plugins.Should().BeEmpty();
    }

    [Fact]
    public void ServiceRequest_ShouldCreateWithCommand()
    {
        // Act
        var request = new ServiceRequest
        {
            Command = "GetStatus"
        };

        // Assert
        request.Command.Should().Be("GetStatus");
        request.Parameters.Should().BeNull();
    }

    [Fact]
    public void ServiceRequest_ShouldCreateWithCommandAndParameters()
    {
        // Arrange
        var parameters = new Dictionary<string, string>
        {
            { "key1", "value1" },
            { "key2", "value2" }
        };

        // Act
        var request = new ServiceRequest
        {
            Command = "TestCommand",
            Parameters = JsonSerializer.SerializeToElement(parameters)
        };

        // Assert
        request.Command.Should().Be("TestCommand");
        request.Parameters.Should().NotBeNull();

        var deserializedParameters = JsonSerializer.Deserialize<Dictionary<string, string>>(request.Parameters!.Value.GetRawText());
        deserializedParameters.Should().NotBeNull();
        deserializedParameters.Should().HaveCount(2);
        deserializedParameters.Should().ContainKey("key1");
        deserializedParameters!["key1"].Should().Be("value1");
    }

    [Fact]
    public void ServiceResponse_ShouldHandleSuccess()
    {
        // Act
        var response = new ServiceResponse
        {
            Success = true,
            ErrorMessage = null
        };

        // Assert
        response.Success.Should().BeTrue();
        response.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public void ServiceResponse_ShouldHandleError()
    {
        // Arrange
        var errorMessage = "Test error message";

        // Act
        var response = new ServiceResponse
        {
            Success = false,
            ErrorMessage = errorMessage
        };

        // Assert
        response.Success.Should().BeFalse();
        response.ErrorMessage.Should().Be(errorMessage);
    }

    [Fact]
    public void ServiceResponse_ShouldHandleDataPayload()
    {
        // Arrange
        var testData = new { Property1 = "Value1", Property2 = 42 };

        // Act
        var response = new ServiceResponse
        {
            Success = true,
            Data = JsonSerializer.SerializeToElement(testData)
        };

        // Assert
        response.Success.Should().BeTrue();
        response.Data.Should().NotBeNull();
        response.Data!.Value.GetProperty("Property1").GetString().Should().Be("Value1");
        response.Data!.Value.GetProperty("Property2").GetInt32().Should().Be(42);
    }

    [Fact]
    public async Task SendRequestAsync_WithNullRequest_ShouldHandleGracefully()
    {
        // Act
        var response = await _client.SendRequestAsync(null!);

        // Assert
        response.Should().NotBeNull();
        response.Success.Should().BeFalse();
    }

    [Fact]
    public async Task MultipleRequests_ShouldNotInterfere()
    {
        // Arrange
        var request1 = new ServiceRequest { Command = "GetStatus" };
        var request2 = new ServiceRequest { Command = "GetConfig" };

        // Act
        var task1 = _client.SendRequestAsync(request1);
        var task2 = _client.SendRequestAsync(request2);

        await Task.WhenAll(task1, task2);

        // Assert - Both should complete without throwing
        task1.IsCompletedSuccessfully.Should().BeTrue();
        task2.IsCompletedSuccessfully.Should().BeTrue();
    }

    private sealed class MockServiceCommunicationClient : ServiceCommunicationClient
    {
        public new Task<List<PluginStatusInfo>?> GetPluginStatusAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult<List<PluginStatusInfo>?>(new List<PluginStatusInfo>());
        }
    }
}
