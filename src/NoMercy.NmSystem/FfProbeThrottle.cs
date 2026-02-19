namespace NoMercy.NmSystem;

/// <summary>
/// Global throttle for ffprobe process instances to prevent spawning too many
/// concurrent ffprobe processes, which can make the system unresponsive.
/// Scales with CPU cores: min 2, max ProcessorCount (capped at 16).
/// </summary>
public static class FfProbeThrottle
{
    public static int MaxConcurrentProbes { get; } = Math.Clamp(Environment.ProcessorCount, 2, 16);

    private static readonly SemaphoreSlim Semaphore = new(MaxConcurrentProbes, MaxConcurrentProbes);

    /// <summary>
    /// Acquires a slot to run an ffprobe process. Blocks until a slot is available.
    /// </summary>
    public static Task WaitAsync(CancellationToken ct = default) => Semaphore.WaitAsync(ct);

    /// <summary>
    /// Acquires a slot asynchronously with a timeout. Returns true if acquired, false on timeout.
    /// </summary>
    public static Task<bool> WaitAsync(TimeSpan timeout, CancellationToken ct = default) => Semaphore.WaitAsync(timeout, ct);

    /// <summary>
    /// Releases a slot after an ffprobe process has completed.
    /// </summary>
    public static void Release() => Semaphore.Release();
}
