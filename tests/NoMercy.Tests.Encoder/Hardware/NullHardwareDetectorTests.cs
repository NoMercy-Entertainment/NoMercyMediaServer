using NoMercy.Encoder.Hardware;

namespace NoMercy.Tests.Encoder.Hardware;

/// <summary>
/// NullHardwareDetector is the safe fallback when real detection isn't
/// available (containers, CI runners, dev). It must report a CPU-only host
/// — never block startup, never throw.
/// </summary>
public class NullHardwareDetectorTests
{
    private readonly NullHardwareDetector _detector = new();

    [Fact]
    public async Task DetectGpusAsync_ReturnsEmpty()
    {
        IReadOnlyList<GpuDevice> gpus = await _detector.DetectGpusAsync();

        gpus.Should().BeEmpty();
    }

    [Fact]
    public async Task DetectCpuCoreCountAsync_MatchesEnvironment()
    {
        int cores = await _detector.DetectCpuCoreCountAsync();

        cores.Should().Be(Environment.ProcessorCount);
    }

    [Fact]
    public async Task DetectCpuCoreCountAsync_AlwaysPositive()
    {
        int cores = await _detector.DetectCpuCoreCountAsync();

        cores.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task DetectGpusAsync_PreCancelledToken_DoesNotThrow()
    {
        // The null implementation returns immediately, so even a pre-cancelled
        // token must not bubble OperationCanceledException — the detector
        // is allowed to ignore cancellation since the work is instantaneous.
        using CancellationTokenSource cts = new();
        await cts.CancelAsync();

        Func<Task> act = () => _detector.DetectGpusAsync(cts.Token);
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task ReturnedGpuList_IsReadOnly()
    {
        IReadOnlyList<GpuDevice> gpus = await _detector.DetectGpusAsync();

        gpus.Should().BeAssignableTo<IReadOnlyList<GpuDevice>>();
    }
}
