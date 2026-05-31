namespace NoMercy.Resources;

/// <summary>
/// An active hold on hardware resources granted by <see cref="IResourceBudget"/>.
/// Release by calling <see cref="IResourceBudget.Release"/> (or dispose the budget
/// wrapper if one is provided). Leases are value types — they must not outlive the
/// <see cref="IResourceBudget"/> that issued them.
/// </summary>
public record ResourceLease(string LeaseId, string? GpuDeviceKey, int GpuSlots, int CpuThreads);
