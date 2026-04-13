namespace NoMercy.Encoder.Hardware;

public record ResourceLease(string LeaseId, GpuDevice? GpuDevice, int GpuSlots, int CpuThreads);
