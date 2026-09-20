namespace StorageWatch.Updater;

internal sealed class ServerRestartCoordinator
{
    private readonly Func<string, bool> _tryRestartServer;
    private readonly Action<string> _log;

    public ServerRestartCoordinator(Func<string, bool> tryRestartServer, Action<string> log)
    {
        _tryRestartServer = tryRestartServer ?? throw new ArgumentNullException(nameof(tryRestartServer));
        _log = log ?? throw new ArgumentNullException(nameof(log));
    }

    public bool TryRestartIfRequested(string checkpointPath, string serviceName)
    {
        if (!CheckpointHandoffStore.TryGetServerRestartDecision(
                checkpointPath,
                out var serverWasRunningBeforeUpdate,
                out var restartServerRequested,
                _log))
        {
            return false;
        }

        _log($"[SERVER-RESTART] Decision: ServerWasRunningBeforeUpdate={serverWasRunningBeforeUpdate}, RestartServerRequested={restartServerRequested}, ServiceName={serviceName}");
        if (!serverWasRunningBeforeUpdate || !restartServerRequested)
        {
            _log("[SERVER-RESTART] SCM start not performed because the persisted restart decision does not require it.");
            return true;
        }

        return _tryRestartServer(serviceName);
    }
}
