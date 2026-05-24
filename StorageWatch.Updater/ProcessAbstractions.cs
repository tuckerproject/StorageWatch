using System.Diagnostics;

namespace StorageWatch.Updater;

internal interface IProcessLauncher
{
    bool Start(ProcessStartInfo startInfo);
}

internal sealed class ProcessLauncher : IProcessLauncher
{
    public bool Start(ProcessStartInfo startInfo)
    {
        return Process.Start(startInfo) != null;
    }
}