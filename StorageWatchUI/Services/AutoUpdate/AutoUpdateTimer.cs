using System;
using System.Threading;
using System.Threading.Tasks;

namespace StorageWatchUI.Services.AutoUpdate
{
    public interface IAutoUpdateTimer : IAsyncDisposable
    {
        ValueTask<bool> WaitForNextTickAsync(CancellationToken cancellationToken);
    }

    public interface IAutoUpdateTimerFactory
    {
        IAutoUpdateTimer Create(TimeSpan interval);
    }

    public sealed class AutoUpdateTimerFactory : IAutoUpdateTimerFactory
    {
        public IAutoUpdateTimer Create(TimeSpan interval) => new PeriodicAutoUpdateTimer(interval);
    }

    internal sealed class PeriodicAutoUpdateTimer : IAutoUpdateTimer
    {
        private readonly PeriodicTimer _timer;

        public PeriodicAutoUpdateTimer(TimeSpan interval)
        {
            _timer = new PeriodicTimer(interval);
        }

        public ValueTask<bool> WaitForNextTickAsync(CancellationToken cancellationToken)
        {
            return _timer.WaitForNextTickAsync(cancellationToken);
        }

        public ValueTask DisposeAsync()
        {
            _timer.Dispose();
            return ValueTask.CompletedTask;
        }
    }
}
