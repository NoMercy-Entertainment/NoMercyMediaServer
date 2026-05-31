using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NoMercy.Encoder.Hardware;
using NoMercy.Resources;

namespace NoMercy.Tests.Encoder.Hardware;

/// <summary>
/// Branch-coverage gaps for <see cref="ProcessResourceMonitor"/>:
///
/// • SampleGpu returns an empty list (no vendor sampler available by default).
/// • SampleGpu logs the "unsupported" warning EXACTLY ONCE — the
///   _gpuWarningLogged flag prevents log spam on repeated calls.
/// • NullResourceMonitor.SampleGpu also returns empty (parity check).
/// • Concurrent GetCpuUsagePercent calls don't crash under the snapshot lock.
/// </summary>
public class ProcessResourceMonitorBranchTests
{
    // ── SampleGpu shape ──────────────────────────────────────────────────────

    [Fact]
    public void SampleGpu_returns_empty_list_without_vendor_sampler()
    {
        ProcessResourceMonitor sut = new(NullLogger<ProcessResourceMonitor>.Instance);

        IReadOnlyList<GpuProcessSample> samples = sut.SampleGpu();

        samples.Should().BeEmpty();
    }

    [Fact]
    public void SampleGpu_logs_unsupported_warning_exactly_once()
    {
        Mock<ILogger<ProcessResourceMonitor>> logger = new();
        ProcessResourceMonitor sut = new(logger.Object);

        sut.SampleGpu();
        sut.SampleGpu();
        sut.SampleGpu();

        // The warning is gated by _gpuWarningLogged — three calls, ONE log.
        logger.Verify(
            l =>
                l.Log(
                    LogLevel.Warning,
                    It.IsAny<EventId>(),
                    It.IsAny<It.IsAnyType>(),
                    It.IsAny<Exception?>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()
                ),
            Times.Once
        );
    }

    [Fact]
    public void SampleGpu_with_null_logger_does_not_throw()
    {
        // Constructor default: ILogger? null — the warning path uses ?.LogWarning
        // so it must be a no-op when no logger is wired.
        ProcessResourceMonitor sut = new();

        Action act = () => sut.SampleGpu();
        act.Should().NotThrow();
    }

    [Fact]
    public void NullResourceMonitor_SampleGpu_returns_empty_list()
    {
        NullResourceMonitor sut = new();
        sut.SampleGpu().Should().BeEmpty();
    }

    // ── Concurrent GetCpuUsagePercent ────────────────────────────────────────

    [Fact]
    public async Task GetCpuUsagePercent_concurrent_calls_do_not_throw()
    {
        // The internal _snapshotLock guards the snapshot/cpuTime fields against
        // torn reads under concurrent calls. Hammer it from multiple threads.
        ProcessResourceMonitor sut = new(NullLogger<ProcessResourceMonitor>.Instance);

        Task[] tasks = Enumerable
            .Range(0, 16)
            .Select(_ =>
                Task.Run(() =>
                {
                    for (int i = 0; i < 100; i++)
                        sut.GetCpuUsagePercent();
                })
            )
            .ToArray();

        Func<Task> act = () => Task.WhenAll(tasks);
        await act.Should().NotThrowAsync();
    }

    // ── GetCpuUsagePercent boundary ─────────────────────────────────────────

    [Fact]
    public void GetCpuUsagePercent_back_to_back_returns_clamped_value()
    {
        // Two back-to-back calls with sub-ms elapsed should NOT yield NaN /
        // negative — the elapsedMs < 1 guard returns 0.
        ProcessResourceMonitor sut = new(NullLogger<ProcessResourceMonitor>.Instance);

        sut.GetCpuUsagePercent();
        double second = sut.GetCpuUsagePercent();

        second.Should().BeGreaterThanOrEqualTo(0);
        second.Should().BeLessThanOrEqualTo(100);
        double.IsNaN(second).Should().BeFalse();
    }
}
