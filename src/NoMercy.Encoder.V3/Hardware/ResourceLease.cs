namespace NoMercy.Encoder.V3.Hardware;

public record ResourceLease(string LeaseId, GpuDevice? GpuDevice, int GpuSlots, int CpuThreads);
