using StorageWatchServer.Server.Services;
using Xunit;

namespace StorageWatchServer.Tests.Services;

public class MachineStatusServiceTests
{
    [Fact]
    public void IsOnline_WithRecentTimestamp_ReturnsTrue()
    {
        // Arrange
        var options = new ServerOptions { OnlineTimeoutMinutes = 5 };
        var service = new MachineStatusService(options);
        var lastSeenUtc = DateTime.UtcNow.AddMinutes(-2);

        // Act
        var isOnline = service.IsOnline(lastSeenUtc);

        // Assert
        Assert.True(isOnline);
    }

    [Fact]
    public void IsOnline_WithOldTimestamp_ReturnsFalse()
    {
        // Arrange
        var options = new ServerOptions { OnlineTimeoutMinutes = 5 };
        var service = new MachineStatusService(options);
        var lastSeenUtc = DateTime.UtcNow.AddMinutes(-10);

        // Act
        var isOnline = service.IsOnline(lastSeenUtc);

        // Assert
        Assert.False(isOnline);
    }

    [Fact]
    public void IsOnline_WithThresholdTimestamp_ReturnsFalse()
    {
        // Arrange
        var options = new ServerOptions { OnlineTimeoutMinutes = 5 };
        var service = new MachineStatusService(options);
        var lastSeenUtc = service.GetOnlineThresholdUtc();

        // Act
        var isOnline = service.IsOnline(lastSeenUtc);

        // Assert
        // At threshold, should be offline
        Assert.False(isOnline);
    }

    [Fact]
    public void IsOnline_JustBeforeThreshold_ReturnsTrue()
    {
        // Arrange
        var options = new ServerOptions { OnlineTimeoutMinutes = 5 };
        var service = new MachineStatusService(options);
        var threshold = service.GetOnlineThresholdUtc();
        var lastSeenUtc = threshold.AddSeconds(1);

        // Act
        var isOnline = service.IsOnline(lastSeenUtc);

        // Assert
        Assert.True(isOnline);
    }

    [Fact]
    public void GetOnlineThresholdUtc_ReturnsCorrectTime()
    {
        // Arrange
        var options = new ServerOptions { OnlineTimeoutMinutes = 10 };
        var service = new MachineStatusService(options);
        var beforeCall = DateTime.UtcNow.AddMinutes(-10);

        // Act
        var threshold = service.GetOnlineThresholdUtc();
        var afterCall = DateTime.UtcNow.AddMinutes(-10);

        // Assert
        Assert.True(threshold >= beforeCall && threshold <= afterCall.AddSeconds(2));
    }
}
