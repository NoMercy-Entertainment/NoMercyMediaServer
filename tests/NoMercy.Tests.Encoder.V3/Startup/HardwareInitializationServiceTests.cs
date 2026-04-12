namespace NoMercy.Tests.Encoder.V3.Startup;

using Microsoft.Extensions.Logging;
using Moq;
using NoMercy.Encoder.V3.Hardware;
using NoMercy.Encoder.V3.Infrastructure;
using NoMercy.Encoder.V3.Startup;

public class HardwareInitializationServiceTests
{
    [Fact]
    public async Task StartAsync_DetectsHardware_SetsCapabilities()
    {
        Mock<IHardwareDetector> detector = new();
        detector
            .Setup(d => d.DetectGpusAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<GpuDevice>());
        detector
            .Setup(d => d.DetectCpuCoreCountAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(4);

        Mock<IProcessRunner> processRunner = new();
        processRunner
            .Setup(r =>
                r.RunAsync(
                    It.IsAny<string>(),
                    It.IsAny<string[]>(),
                    It.IsAny<string?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(new ProcessResult(0, "", "", TimeSpan.Zero));

        FfmpegCapabilities ffmpegCaps = new(processRunner.Object);

        HardwareInitializationService service = new(
            detector.Object,
            ffmpegCaps,
            Mock.Of<ILogger<HardwareInitializationService>>()
        );

        await service.StartAsync(CancellationToken.None);

        service.IsReady.Should().BeTrue();
        service.Capabilities.Should().NotBeNull();
        service.Capabilities!.CpuCores.Should().Be(4);
        service.Capabilities.HasGpu.Should().BeFalse();
    }

    [Fact]
    public async Task StartAsync_IsNotReadyBeforeStart()
    {
        Mock<IHardwareDetector> detector = new();
        detector
            .Setup(d => d.DetectGpusAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<GpuDevice>());
        detector
            .Setup(d => d.DetectCpuCoreCountAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(8);

        Mock<IProcessRunner> processRunner = new();
        processRunner
            .Setup(r =>
                r.RunAsync(
                    It.IsAny<string>(),
                    It.IsAny<string[]>(),
                    It.IsAny<string?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(new ProcessResult(0, "", "", TimeSpan.Zero));

        FfmpegCapabilities ffmpegCaps = new(processRunner.Object);

        HardwareInitializationService service = new(
            detector.Object,
            ffmpegCaps,
            Mock.Of<ILogger<HardwareInitializationService>>()
        );

        service.IsReady.Should().BeFalse();

        await service.StartAsync(CancellationToken.None);

        service.IsReady.Should().BeTrue();
    }

    [Fact]
    public async Task StartAsync_OnDetectionFailure_FallsBackToSoftware()
    {
        Mock<IHardwareDetector> detector = new();
        detector
            .Setup(d => d.DetectGpusAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("GPU probe exploded"));

        Mock<IProcessRunner> processRunner = new();
        processRunner
            .Setup(r =>
                r.RunAsync(
                    It.IsAny<string>(),
                    It.IsAny<string[]>(),
                    It.IsAny<string?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(new ProcessResult(0, "", "", TimeSpan.Zero));

        FfmpegCapabilities ffmpegCaps = new(processRunner.Object);

        HardwareInitializationService service = new(
            detector.Object,
            ffmpegCaps,
            Mock.Of<ILogger<HardwareInitializationService>>()
        );

        await service.StartAsync(CancellationToken.None);

        service.IsReady.Should().BeTrue();
        service.Capabilities.Should().NotBeNull();
        service.Capabilities!.HasGpu.Should().BeFalse();
        service.Capabilities.CpuCores.Should().Be(Environment.ProcessorCount);
    }

    [Fact]
    public async Task StopAsync_CompletesImmediately()
    {
        Mock<IHardwareDetector> detector = new();
        detector
            .Setup(d => d.DetectGpusAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<GpuDevice>());
        detector
            .Setup(d => d.DetectCpuCoreCountAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(4);

        Mock<IProcessRunner> processRunner = new();
        processRunner
            .Setup(r =>
                r.RunAsync(
                    It.IsAny<string>(),
                    It.IsAny<string[]>(),
                    It.IsAny<string?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(new ProcessResult(0, "", "", TimeSpan.Zero));

        FfmpegCapabilities ffmpegCaps = new(processRunner.Object);

        HardwareInitializationService service = new(
            detector.Object,
            ffmpegCaps,
            Mock.Of<ILogger<HardwareInitializationService>>()
        );

        await service.StartAsync(CancellationToken.None);
        await service.StopAsync(CancellationToken.None);

        service.IsReady.Should().BeTrue();
    }
}
