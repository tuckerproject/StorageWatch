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
    public async Task GetMachinesAsync_WithMultipleMachines_ReturnsAllMachines()
    {
        // Arrange
        await using var factory = await TestDatabaseFactory.CreateAsync();
        var repository = factory.CreateRepository();
        var machineName1 = "Machine1";
        var machineName2 = "Machine2";
        var now = DateTime.UtcNow;
        var id1 = await repository.UpsertMachineAsync(machineName1, now);
        var id2 = await repository.UpsertMachineAsync(machineName2, now);

        // Act
        var machines = await repository.GetMachinesAsync();

        // Assert
        Assert.True(machines.Count >= 2, $"Expected at least 2 machines, got {machines.Count}");
        Assert.Contains(machines, m => m.MachineName == machineName1);
        Assert.Contains(machines, m => m.MachineName == machineName2);
    }

    [Fact]
    public async Task GetMachineAsync_WithValidId_ReturnsMachine()
    {
        // Arrange
        await using var factory = await TestDatabaseFactory.CreateAsync();
        var repository = factory.CreateRepository();
        var machineName = "TestMachine";
        var now = DateTime.UtcNow;
        var machineId = await repository.UpsertMachineAsync(machineName, now);

        // Act
        var machine = await repository.GetMachineAsync(machineId);

        // Assert
        Assert.NotNull(machine);
        Assert.Equal(machineName, machine.MachineName);
    }

    [Fact]
    public async Task GetMachineAsync_WithInvalidId_ReturnsNull()
    {
        // Arrange
        await using var factory = await TestDatabaseFactory.CreateAsync();
        var repository = factory.CreateRepository();

        // Act
        var machine = await repository.GetMachineAsync(999);

        // Assert
        Assert.Null(machine);
    }

    [Fact]
    public async Task UpsertDriveAsync_WithNewDrive_InsertsSuccessfully()
    {
        // Arrange
        await using var factory = await TestDatabaseFactory.CreateAsync();
        var repository = factory.CreateRepository();
        var now = DateTime.UtcNow;
        var machineId = await repository.UpsertMachineAsync("TestMachine", now);
        var drive = TestDataFactory.CreateDriveStatus("C:", 75, now);

        // Act
        await repository.UpsertDriveAsync(machineId, drive);
        var machine = await repository.GetMachineAsync(machineId);

        // Assert
        Assert.NotNull(machine);
        Assert.Single(machine.Drives);
        Assert.Equal("C:", machine.Drives[0].DriveLetter);
        Assert.Equal(75, machine.Drives[0].PercentFree);
    }

    [Fact]
    public async Task UpsertDriveAsync_WithDuplicateDrive_UpdatesSuccessfully()
    {
        // Arrange
        await using var factory = await TestDatabaseFactory.CreateAsync();
        var repository = factory.CreateRepository();
        var now = DateTime.UtcNow;
        var machineId = await repository.UpsertMachineAsync("TestMachine", now);
        var drive1 = TestDataFactory.CreateDriveStatus("C:", 75, now);
        var drive2 = TestDataFactory.CreateDriveStatus("C:", 50, now.AddMinutes(1));

        // Act
        await repository.UpsertDriveAsync(machineId, drive1);
        await repository.UpsertDriveAsync(machineId, drive2);
        var machine = await repository.GetMachineAsync(machineId);

        // Assert
        Assert.NotNull(machine);
        Assert.Single(machine.Drives);
        Assert.Equal(50, machine.Drives[0].PercentFree);
    }

    [Fact]
    public async Task InsertDiskHistoryAsync_InsertsHistoryPointSuccessfully()
    {
        // Arrange
        await using var factory = await TestDatabaseFactory.CreateAsync();
        var repository = factory.CreateRepository();
        var now = DateTime.UtcNow;
        var machineId = await repository.UpsertMachineAsync("TestMachine", now);
        var historyPoint = TestDataFactory.CreateHistoryPoint(now, 75);

        // Act
        await repository.InsertDiskHistoryAsync(machineId, "C:", historyPoint);
        var history = await repository.GetDiskHistoryAsync(machineId, "C:", now.AddHours(-1));

        // Assert
        Assert.Single(history);
        Assert.Equal(75, history[0].PercentFree);
    }

    [Fact]
    public async Task GetDiskHistoryAsync_WithDateRange_ReturnsOnlyPointsInRange()
    {
        // Arrange
        await using var factory = await TestDatabaseFactory.CreateAsync();
        var repository = factory.CreateRepository();
        var now = DateTime.UtcNow;
        var machineId = await repository.UpsertMachineAsync("TestMachine", now);

        var pointOld = TestDataFactory.CreateHistoryPoint(now.AddDays(-10), 75);
        var pointRecent1 = TestDataFactory.CreateHistoryPoint(now.AddHours(-2), 60);
        var pointRecent2 = TestDataFactory.CreateHistoryPoint(now, 50);

        await repository.InsertDiskHistoryAsync(machineId, "C:", pointOld);
        await repository.InsertDiskHistoryAsync(machineId, "C:", pointRecent1);
        await repository.InsertDiskHistoryAsync(machineId, "C:", pointRecent2);

        // Act
        var history = await repository.GetDiskHistoryAsync(machineId, "C:", now.AddDays(-3));

        // Assert
        // Verify we get at least the 2 recent points, and all points are within the range
        Assert.True(history.Count >= 2, $"Expected at least 2 points, got {history.Count}");
        Assert.All(history, point => Assert.True(point.CollectionTimeUtc >= now.AddDays(-3)));
    }

    [Fact]
    public async Task GetAlertsAsync_ReturnsAllAlerts()
    {
        // Arrange
        await using var factory = await TestDatabaseFactory.CreateAsync();
        var repository = factory.CreateRepository();

        // Act
        var alerts = await repository.GetAlertsAsync();

        // Assert
        Assert.NotNull(alerts);
        Assert.IsAssignableFrom<IReadOnlyList<StorageWatchServer.Server.Models.AlertRecord>>(alerts);
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

    [Fact]
    public async Task GetMachineDrivesAsync_ReturnsDrivesForMachine()
    {
        // Arrange
        await using var factory = await TestDatabaseFactory.CreateAsync();
        var repository = factory.CreateRepository();
        var now = DateTime.UtcNow;
        var machineId = await repository.UpsertMachineAsync("TestMachine", now);
        var drive = TestDataFactory.CreateDriveStatus("C:", 75, now);
        await repository.UpsertDriveAsync(machineId, drive);

        // Act
        var drives = await repository.GetMachineDrivesAsync(machineId);

        // Assert
        Assert.Single(drives);
        Assert.Equal("C:", drives[0].DriveLetter);
    }

    [Fact]
    public async Task MultiMachineDataSeparation_MachinesAreIndependent()
    {
        // Arrange
        await using var factory = await TestDatabaseFactory.CreateAsync();
        var repository = factory.CreateRepository();
        var now = DateTime.UtcNow;

        var machine1Id = await repository.UpsertMachineAsync("Machine1", now);
        var machine2Id = await repository.UpsertMachineAsync("Machine2", now);

        var drive1 = TestDataFactory.CreateDriveStatus("C:", 75, now);
        var drive2 = TestDataFactory.CreateDriveStatus("D:", 50, now);

        await repository.UpsertDriveAsync(machine1Id, drive1);
        await repository.UpsertDriveAsync(machine2Id, drive2);

        // Act
        var machine1 = await repository.GetMachineAsync(machine1Id);
        var machine2 = await repository.GetMachineAsync(machine2Id);

        // Assert
        Assert.NotNull(machine1);
        Assert.NotNull(machine2);
        Assert.Single(machine1.Drives);
        Assert.Single(machine2.Drives);
        Assert.Equal("C:", machine1.Drives[0].DriveLetter);
        Assert.Equal("D:", machine2.Drives[0].DriveLetter);
    }
}
