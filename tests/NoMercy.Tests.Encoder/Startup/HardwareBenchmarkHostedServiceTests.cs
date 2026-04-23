namespace NoMercy.Tests.Encoder.Startup;

using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NoMercy.Encoder.Composition;
using NoMercy.Encoder.Hardware;
using NoMercy.Encoder.Startup;

public class HardwareBenchmarkHostedServiceTests
{
    [Fact]
    public async Task StartAsync_AutoCalibrateOff_SkipsBenchmark()
    {
        Mock<IHardwareBenchmark> benchmark = new();
        HardwareBenchmarkHostedService sut = NewService(benchmark.Object, autoCalibrate: false);

        await sut.StartAsync(CancellationToken.None);

        benchmark.Verify(b => b.CalibrateAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task StartAsync_FreshCache_SkipsBenchmark()
    {
        Mock<IHardwareBenchmark> benchmark = new();
        benchmark.Setup(b => b.NeedsRecalibration()).Returns(false);

        HardwareBenchmarkHostedService sut = NewService(benchmark.Object, autoCalibrate: true);

        await sut.StartAsync(CancellationToken.None);

        benchmark.Verify(b => b.CalibrateAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task StartAsync_StaleCache_TriggersCalibrationInBackground()
    {
        TaskCompletionSource<bool> calibrationStarted = new();
        Mock<IHardwareBenchmark> benchmark = new();
        benchmark.Setup(b => b.NeedsRecalibration()).Returns(true);
        benchmark
            .Setup(b => b.CalibrateAsync(It.IsAny<CancellationToken>()))
            .Returns(
                (CancellationToken _) =>
                {
                    calibrationStarted.TrySetResult(true);
                    return Task.FromResult(
                        new SpeedIndex(new())
                    );
                }
            );

        HardwareBenchmarkHostedService sut = NewService(benchmark.Object, autoCalibrate: true);

        await sut.StartAsync(CancellationToken.None);

        // StartAsync must return fast — the benchmark runs on a background task.
        bool started =
            await Task.WhenAny(calibrationStarted.Task, Task.Delay(2000))
            == calibrationStarted.Task;
        started.Should().BeTrue("benchmark should kick off asynchronously");

        await sut.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task StopAsync_CancelsInFlightBenchmark()
    {
        TaskCompletionSource<bool> cancelSeen = new();
        Mock<IHardwareBenchmark> benchmark = new();
        benchmark.Setup(b => b.NeedsRecalibration()).Returns(true);
        benchmark
            .Setup(b => b.CalibrateAsync(It.IsAny<CancellationToken>()))
            .Returns(
                async (CancellationToken ct) =>
                {
                    try
                    {
                        await Task.Delay(TimeSpan.FromMinutes(5), ct);
                    }
                    catch (OperationCanceledException)
                    {
                        cancelSeen.TrySetResult(true);
                        throw;
                    }
                    return new(new());
                }
            );

        HardwareBenchmarkHostedService sut = NewService(benchmark.Object, autoCalibrate: true);
        await sut.StartAsync(CancellationToken.None);

        await sut.StopAsync(CancellationToken.None);

        bool cancelled = await Task.WhenAny(cancelSeen.Task, Task.Delay(2000)) == cancelSeen.Task;
        cancelled.Should().BeTrue("running benchmark should receive cancellation on shutdown");
    }

    private static HardwareBenchmarkHostedService NewService(
        IHardwareBenchmark benchmark,
        bool autoCalibrate
    ) =>
        new(
            benchmark,
            new()
            {
                FfmpegPathOverride = "ffmpeg",
                FfprobePathOverride = "ffprobe",
                AutoCalibrate = autoCalibrate,
            },
            NullLogger<HardwareBenchmarkHostedService>.Instance
        );
}
