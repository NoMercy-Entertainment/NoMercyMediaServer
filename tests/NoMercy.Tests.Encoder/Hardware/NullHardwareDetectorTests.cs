namespace NoMercy.Tests.Encoder.Hardware;

using NoMercy.Encoder.Hardware;

public class NullHardwareDetectorTests
{
    [Fact]
    public async Task NullDetector_ReturnsNoGpus()
    {
        NullHardwareDetector detector = new();
        IReadOnlyList<GpuDevice> gpus = await detector.DetectGpusAsync();
        gpus.Should().BeEmpty();
    }

    [Fact]
    public async Task NullDetector_ReturnsCpuCoreCount()
    {
        NullHardwareDetector detector = new();
        int cores = await detector.DetectCpuCoreCountAsync();
        cores.Should().BeGreaterThan(0);
    }
}
