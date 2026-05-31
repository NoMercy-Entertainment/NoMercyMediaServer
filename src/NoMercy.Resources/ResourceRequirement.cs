namespace NoMercy.Resources;

/// <summary>
/// Declares the hardware resources required to run one encode task.
/// <see cref="GpuDeviceKey"/> is the canonical name string from
/// <c>GpuDevice.Name</c>; null means CPU-only.
/// </summary>
public record ResourceRequirement(string? GpuDeviceKey, int GpuSlots, int CpuThreads);
