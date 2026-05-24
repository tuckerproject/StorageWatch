using System.Diagnostics;
using StorageWatch.Updater;

namespace StorageWatch.Updater.Tests.Helpers;

public sealed class FakeProcessLauncher : IProcessLauncher
{
    private readonly Queue<bool> _results = new();

    public List<ProcessStartInfo> StartedProcesses { get; } = new();

    public void EnqueueResult(bool result)
    {
        _results.Enqueue(result);
    }

    public bool Start(ProcessStartInfo startInfo)
    {
        StartedProcesses.Add(startInfo);

        if (_results.Count == 0)
        {
            return true;
        }

        return _results.Dequeue();
    }
}
