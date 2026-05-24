using StorageWatchServer.Server.Api;
using StorageWatchServer.Tests.Utilities;
using Xunit;

namespace StorageWatchServer.Tests.Data;

public class ServerRepositoryTests
{
    [Fact]
    public async Task UpsertMachineAsync_WithNewMachine_InsertsAndReturnsId()
    {
        // Arrange
        await using var factory = await TestDatabaseFactory.CreateAsync();
        var repository = factory.CreateRepository();
        var machineName = "TestMachine";
        var lastSeenUtc = DateTime.UtcNow;

        // Act
        var machineId = await repository.UpsertMachineAsync(machineName, lastSeenUtc);

        // Assert
        Assert.True(machineId > 0);
    }

    [Fact]
    public async Task UpsertMachineAsync_WithDuplicateMachine_UpdatesLastSeenAndReturnsSameId()
    {
        // Arrange
        await using var factory = await TestDatabaseFactory.CreateAsync();
        var repository = factory.CreateRepository();
        var machineName = "TestMachine";
        var firstTime = DateTime.UtcNow.AddHours(-1);
        var secondTime = DateTime.UtcNow;

        // Act
        var firstId = await repository.UpsertMachineAsync(machineName, firstTime);
        var secondId = await repository.UpsertMachineAsync(machineName, secondTime);

        // Assert
        Assert.Equal(firstId, secondId);
    }


    [Fact]
    public async Task GetSettingsAsync_ReturnsAllSettings()
    {
        // Arrange
        await using var factory = await TestDatabaseFactory.CreateAsync();
        var repository = factory.CreateRepository();

        // Act
        var settings = await repository.GetSettingsAsync();

        // Assert
        Assert.NotNull(settings);
        Assert.True(settings.Count > 0);
        Assert.Contains(settings, s => s.Key == "OnlineTimeoutMinutes");
    }

}
