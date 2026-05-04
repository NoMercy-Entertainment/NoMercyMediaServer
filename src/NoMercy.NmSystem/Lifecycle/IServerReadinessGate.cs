namespace NoMercy.NmSystem.Lifecycle;

/// <summary>
/// Guards queue workers from pulling jobs until the server is fully started
/// and all registered readiness signals have resolved.
/// </summary>
public interface IServerReadinessGate
{
    /// <summary>
    /// Resolves when the server is fully ready to process queue jobs:
    /// host ApplicationStarted has fired AND all registered readiness
    /// signals have completed.
    /// </summary>
    Task WaitForReadyAsync(CancellationToken ct);

    /// <summary>
    /// Register an additional async signal that must complete before
    /// WaitForReadyAsync resolves. Call from hosted services during
    /// their own StartAsync — never AFTER WaitForReadyAsync has been
    /// awaited.
    /// </summary>
    void AddSignal(string name, Task signal);
}
