namespace NoMercy.Encoder.V3.Hardware;

public record ResourceRequirement(GpuDevice? GpuDevice, int GpuSlots, int CpuThreads);
