using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace StorageWatchServer.Server.Data;

public sealed class ServerDatabaseShutdownCoordinator : IHostedService
{
    private readonly ILogger<ServerDatabaseShutdownCoordinator> _logger;
    private readonly TaskCompletionSource _drained = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int _activeOperations;
    private int _stopping;

    public ServerDatabaseShutdownCoordinator(ILogger<ServerDatabaseShutdownCoordinator>? logger = null)
    {
        _logger = logger ?? NullLogger<ServerDatabaseShutdownCoordinator>.Instance;
    }

    public ValueTask<IAsyncDisposable> BeginOperationAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (Volatile.Read(ref _stopping) == 1)
        {
            throw new OperationCanceledException("Server shutdown is in progress. New database operations are not allowed.", cancellationToken);
        }

        Interlocked.Increment(ref _activeOperations);

        if (Volatile.Read(ref _stopping) == 1)
        {
            CompleteOperation();
            throw new OperationCanceledException("Server shutdown is in progress. New database operations are not allowed.", cancellationToken);
        }

        return ValueTask.FromResult<IAsyncDisposable>(new OperationLease(this));
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (Interlocked.Exchange(ref _stopping, 1) == 1)
        {
            return;
        }

        var activeOperations = Volatile.Read(ref _activeOperations);
        _logger.LogInformation("[SHUTDOWN] Database shutdown started. Waiting for {ActiveOperations} active database operation(s) to complete.", activeOperations);

        if (activeOperations == 0)
        {
            _drained.TrySetResult();
            _logger.LogInformation("[SHUTDOWN] No active database operations remain. Database resources are ready for shutdown.");
            return;
        }

        using var registration = cancellationToken.Register(() => _drained.TrySetCanceled(cancellationToken));

        try
        {
            await _drained.Task.ConfigureAwait(false);
            _logger.LogInformation("[SHUTDOWN] All active database operations completed. Database resources are ready for shutdown.");
        }
        catch (OperationCanceledException)
        {
            var remainingOperations = Volatile.Read(ref _activeOperations);
            _logger.LogWarning("[SHUTDOWN] Shutdown completed before all database operations drained. {RemainingOperations} operation(s) may still be in progress.", remainingOperations);
        }
    }

    private void CompleteOperation()
    {
        var remainingOperations = Interlocked.Decrement(ref _activeOperations);
        if (remainingOperations == 0 && Volatile.Read(ref _stopping) == 1)
        {
            _drained.TrySetResult();
        }
    }

    private sealed class OperationLease : IAsyncDisposable, IDisposable
    {
        private readonly ServerDatabaseShutdownCoordinator _owner;
        private int _disposed;

        public OperationLease(ServerDatabaseShutdownCoordinator owner)
        {
            _owner = owner;
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0)
            {
                _owner.CompleteOperation();
            }
        }

        public ValueTask DisposeAsync()
        {
            Dispose();
            return ValueTask.CompletedTask;
        }
    }
}
