namespace NoMercy.Encoder.V3.Hardware;

public interface IHardwareDetector
{
    Task<IReadOnlyList<GpuDevice>> DetectGpusAsync(CancellationToken ct = default);
    Task<int> DetectCpuCoreCountAsync(CancellationToken ct = default);
}
