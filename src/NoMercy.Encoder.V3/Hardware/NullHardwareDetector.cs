namespace NoMercy.Encoder.V3.Hardware;

public class NullHardwareDetector : IHardwareDetector
{
    public Task<IReadOnlyList<GpuDevice>> DetectGpusAsync(CancellationToken ct = default)
    {
        IReadOnlyList<GpuDevice> empty = [];
        return Task.FromResult(empty);
    }

    public Task<int> DetectCpuCoreCountAsync(CancellationToken ct = default)
    {
        return Task.FromResult(Environment.ProcessorCount);
    }
}
