using Microsoft.Extensions.Logging.Abstractions;
using StorageWatchServer.Server.Data;
using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace StorageWatchServer.Tests.Services;

public class DatabaseShutdownCoordinatorTests
{
    [Fact]
    public async Task StopAsync_WaitsForActiveDatabaseOperationsToComplete()
    {
        var coordinator = new ServerDatabaseShutdownCoordinator(NullLogger<ServerDatabaseShutdownCoordinator>.Instance);
        await using var operation = await coordinator.BeginOperationAsync();

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var stopTask = coordinator.StopAsync(cts.Token);

        Assert.False(stopTask.IsCompleted);

        await operation.DisposeAsync();
        await stopTask;
    }

    [Fact]
    public async Task BeginOperationAsync_RejectsNewDatabaseOperationsAfterShutdownStarts()
    {
        var coordinator = new ServerDatabaseShutdownCoordinator(NullLogger<ServerDatabaseShutdownCoordinator>.Instance);

        await coordinator.StopAsync(CancellationToken.None);

        await Assert.ThrowsAsync<OperationCanceledException>(async () => await coordinator.BeginOperationAsync());
    }
}
