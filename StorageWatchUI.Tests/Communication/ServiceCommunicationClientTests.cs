using Xunit;
using StorageWatchUI.Communication;
using System.Threading.Tasks;

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
        var status = await _client.GetStatusAsync();
        
        // Service likely not running in test environment
        // Should handle gracefully
        Assert.True(status == null);
    }

    [Fact]
    public async Task SendRequestAsync_WithInvalidCommand_ShouldReturnError()
    {
        var request = new ServiceRequest
        {
            Command = "InvalidCommand"
        };

        var response = await _client.SendRequestAsync(request);
        
        // Should get a response indicating error or timeout
        Assert.False(response.Success);
    }

    [Fact]
    public void ServiceRequest_ShouldCreateWithCommand()
    {
        var request = new ServiceRequest
        {
            Command = "GetStatus"
        };

        Assert.Equal("GetStatus", request.Command);
        Assert.Null(request.Parameters);
    }

    [Fact]
    public void ServiceResponse_ShouldHandleSuccess()
    {
        var response = new ServiceResponse
        {
            Success = true,
            ErrorMessage = null
        };

        Assert.True(response.Success);
        Assert.Null(response.ErrorMessage);
    }

    [Fact]
    public void ServiceResponse_ShouldHandleError()
    {
        var response = new ServiceResponse
        {
            Success = false,
            ErrorMessage = "Test error"
        };

        Assert.False(response.Success);
        Assert.Equal("Test error", response.ErrorMessage);
    }
}
