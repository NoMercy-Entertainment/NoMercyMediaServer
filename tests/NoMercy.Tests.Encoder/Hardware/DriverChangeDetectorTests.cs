using Moq;
using NoMercy.Encoder.Codecs;
using NoMercy.Encoder.Hardware;

namespace NoMercy.Tests.Encoder.Hardware;

public class DriverChangeDetectorTests
{
    private static IReadOnlyList<GpuDevice> OneGpu(string? driver = "31.0.15.4601") =>
        [
            new GpuDevice(
                GpuVendor.Nvidia,
                "RTX 4090",
                VramMb: 24576,
                MaxEncoderSessions: 8,
                SupportedCodecs: [VideoCodecType.H264, VideoCodecType.H265],
                DriverVersion: driver
            ),
        ];

    [Fact]
    public async Task DetectAndPersistAsync_FirstBoot_ReturnsIsFirstBootTrueChangedFalse()
    {
        Mock<IHardwareDetector> detector = new();
        detector
            .Setup(d => d.DetectGpusAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(OneGpu());

        Mock<IDriverFingerprintStore> store = new();
        store
            .Setup(s => s.LoadHashAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);
        store
            .Setup(s => s.SaveHashAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        DriverChangeDetector sut = new(detector.Object, store.Object);

        DriverChangeResult result = await sut.DetectAndPersistAsync();

        result.IsFirstBoot.Should().BeTrue();
        result.Changed.Should().BeFalse();
        result.PreviousHash.Should().BeNull();
        result.CurrentHash.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task DetectAndPersistAsync_DifferentHash_ReturnsChanged()
    {
        Mock<IHardwareDetector> detector = new();
        detector
            .Setup(d => d.DetectGpusAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(OneGpu("31.0.15.5000"));

        Mock<IDriverFingerprintStore> store = new();
        store
            .Setup(s => s.LoadHashAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync("aabbccdd00112233aabbccdd00112233aabbccdd00112233aabbccdd00112233");
        store
            .Setup(s => s.SaveHashAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        DriverChangeDetector sut = new(detector.Object, store.Object);

        DriverChangeResult result = await sut.DetectAndPersistAsync();

        result.Changed.Should().BeTrue();
        result.IsFirstBoot.Should().BeFalse();
    }

    [Fact]
    public async Task DetectAndPersistAsync_SameHash_ReturnsNotChanged()
    {
        IReadOnlyList<GpuDevice> gpus = OneGpu();

        Mock<IHardwareDetector> detector = new();
        detector.Setup(d => d.DetectGpusAsync(It.IsAny<CancellationToken>())).ReturnsAsync(gpus);

        // Compute the expected hash so the stored hash matches
        DriverFingerprint fp = new([new GpuDriverInfo("Nvidia", "RTX 4090", "31.0.15.4601", 0)]);
        string expectedHash = fp.ComputeHash();

        Mock<IDriverFingerprintStore> store = new();
        store.Setup(s => s.LoadHashAsync(It.IsAny<CancellationToken>())).ReturnsAsync(expectedHash);
        store
            .Setup(s => s.SaveHashAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        DriverChangeDetector sut = new(detector.Object, store.Object);

        DriverChangeResult result = await sut.DetectAndPersistAsync();

        result.Changed.Should().BeFalse();
        result.IsFirstBoot.Should().BeFalse();
    }

    [Fact]
    public async Task DetectAndPersistAsync_AlwaysPersistsCurrentHash()
    {
        Mock<IHardwareDetector> detector = new();
        detector
            .Setup(d => d.DetectGpusAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(OneGpu());

        Mock<IDriverFingerprintStore> store = new();
        store
            .Setup(s => s.LoadHashAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);

        string? savedHash = null;
        store
            .Setup(s => s.SaveHashAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, CancellationToken>((h, _) => savedHash = h)
            .Returns(Task.CompletedTask);

        DriverChangeDetector sut = new(detector.Object, store.Object);

        DriverChangeResult result = await sut.DetectAndPersistAsync();

        store.Verify(
            s => s.SaveHashAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Once
        );
        savedHash.Should().Be(result.CurrentHash);
    }
}
