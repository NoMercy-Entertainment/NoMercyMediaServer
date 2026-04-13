namespace NoMercy.Encoder.Hardware;

public record ResourceRequirement(GpuDevice? GpuDevice, int GpuSlots, int CpuThreads);
