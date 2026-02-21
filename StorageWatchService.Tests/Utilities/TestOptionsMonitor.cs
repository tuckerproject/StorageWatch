using Microsoft.Extensions.Options;

namespace StorageWatch.Tests.Utilities
{
    public class TestOptionsMonitor<T> : IOptionsMonitor<T> where T : class
    {
        public TestOptionsMonitor(T currentValue)
        {
            CurrentValue = currentValue;
        }

        public T CurrentValue { get; private set; }

        public T Get(string? name) => CurrentValue;

        public IDisposable OnChange(Action<T, string?> listener) => new NoOpDisposable();

        public void Update(T value) => CurrentValue = value;

        private sealed class NoOpDisposable : IDisposable
        {
            public void Dispose() { }
        }
    }
}
