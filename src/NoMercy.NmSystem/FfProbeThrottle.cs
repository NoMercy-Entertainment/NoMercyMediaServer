namespace NoMercy.NmSystem;

/// <summary>
/// Global throttle for ffprobe process instances to prevent spawning too many
/// concurrent ffprobe processes, which can crash GPU drivers and make the system unresponsive.
/// </summary>
public static class FfProbeThrottle
{
    private static readonly SemaphoreSlim Semaphore = new(MaxConcurrentProbes, MaxConcurrentProbes);

    /// <summary>
    /// Maximum number of ffprobe processes allowed to run concurrently across the entire application.
    /// </summary>
    private const int MaxConcurrentProbes = 3;

    /// <summary>
    /// Acquires a slot to run an ffprobe process. Blocks until a slot is available.
    /// </summary>
    public static Task WaitAsync(CancellationToken ct = default) => Semaphore.WaitAsync(ct);

    /// <summary>
    /// Releases a slot after an ffprobe process has completed.
    /// </summary>
    public static void Release() => Semaphore.Release();
}
