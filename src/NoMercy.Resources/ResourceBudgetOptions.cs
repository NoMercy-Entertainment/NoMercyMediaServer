namespace NoMercy.Resources;

/// <summary>
/// Live-headroom thresholds consulted by <see cref="IResourceBudget.TryAcquire"/>
/// before granting a new encoder lease. The static semaphores still enforce
/// hardware ceilings (NVENC session caps, CPU thread count) — these
/// thresholds are the dynamic throttle on top, so a 16-core box with 8
/// CPU-thread budget doesn't actually launch 8 encodes when 4 already pin
/// the host.
///
/// Any threshold left at or below 0 (or <c>MinFreeMemoryMb</c> at 0) is
/// treated as disabled. Default values err on the side of "leave headroom
/// for the user's other work" — they don't max out the box.
/// </summary>
public sealed record ResourceBudgetOptions(
    double CpuHeadroomPercent = 75.0,
    double GpuHeadroomPercent = 80.0,
    long MinFreeMemoryMb = 1024
)
{
    /// <summary>
    /// Permissive defaults that disable every live-headroom check. Used by
    /// the legacy ResourceBudget constructor + tests that don't want to
    /// model live host load.
    /// </summary>
    public static ResourceBudgetOptions Disabled { get; } =
        new(CpuHeadroomPercent: 0, GpuHeadroomPercent: 0, MinFreeMemoryMb: 0);
}
