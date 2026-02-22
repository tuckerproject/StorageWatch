using Microsoft.Extensions.Options;

namespace StorageWatchUI.Tests.Utilities
{
    public class TestOptionsMonitor<T> : IOptionsMonitor<T>, IOptions<T> where T : class
    {
        public TestOptionsMonitor(T currentValue)
        {
            CurrentValue = currentValue;
            Value = currentValue;
        }

        public T CurrentValue { get; private set; }

        public T Value { get; private set; }

        public T Get(string? name) => CurrentValue;

        public IDisposable OnChange(Action<T, string?> listener) => new NoOpDisposable();

        private sealed class NoOpDisposable : IDisposable
        {
            public void Dispose() { }
        }
    }
}
