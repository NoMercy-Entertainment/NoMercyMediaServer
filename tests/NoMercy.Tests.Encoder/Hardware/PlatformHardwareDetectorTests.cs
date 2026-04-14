namespace NoMercy.Tests.Encoder.Hardware;

using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using Moq;
using NoMercy.Encoder.Codecs;
using NoMercy.Encoder.Hardware;
using NoMercy.Encoder.Infrastructure;

public class PlatformHardwareDetectorTests
{
    private readonly Mock<IProcessRunner> _processRunner = new();
    private readonly Mock<IFfmpegCapabilities> _capabilities = new();
    private readonly Mock<ILogger<PlatformHardwareDetector>> _logger = new();

    private PlatformHardwareDetector CreateDetector() =>
        new(_processRunner.Object, _capabilities.Object, _logger.Object);

    [Fact]
    public async Task DetectCpuCoreCount_ReturnsProcessorCount()
    {
        PlatformHardwareDetector detector = CreateDetector();
        int cores = await detector.DetectCpuCoreCountAsync();
        cores.Should().Be(Environment.ProcessorCount);
    }

    [Fact]
    public async Task DetectGpus_Windows_ParsesWmicOutput()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return;

        string wmicOutput = "Node,AdapterRAM,Name\nPC,8589934592,NVIDIA GeForce RTX 3070\n";

        _processRunner
            .Setup(p =>
                p.RunAsync(
                    "wmic",
                    It.IsAny<string[]>(),
                    It.IsAny<string?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(new ProcessResult(0, wmicOutput, "", TimeSpan.Zero));

        _capabilities.Setup(c => c.HasEncoder("h264_nvenc")).Returns(true);
        _capabilities.Setup(c => c.HasEncoder("hevc_nvenc")).Returns(true);

        PlatformHardwareDetector detector = CreateDetector();
        IReadOnlyList<GpuDevice> gpus = await detector.DetectGpusAsync();

        gpus.Should().HaveCount(1);
        gpus[0].Vendor.Should().Be(GpuVendor.Nvidia);
        gpus[0].Name.Should().Contain("RTX 3070");
        gpus[0].VramMb.Should().Be(8192);
        gpus[0].MaxEncoderSessions.Should().Be(8);
        gpus[0].SupportedCodecs.Should().Contain(VideoCodecType.H264);
        gpus[0].SupportedCodecs.Should().Contain(VideoCodecType.H265);
    }

    [Fact]
    public async Task DetectGpus_Windows_SkipsNonGpuAdapters()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return;

        string wmicOutput = "Node,AdapterRAM,Name\nPC,0,Microsoft Basic Display Adapter\n";

        _processRunner
            .Setup(p =>
                p.RunAsync(
                    "wmic",
                    It.IsAny<string[]>(),
                    It.IsAny<string?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(new ProcessResult(0, wmicOutput, "", TimeSpan.Zero));

        PlatformHardwareDetector detector = CreateDetector();
        IReadOnlyList<GpuDevice> gpus = await detector.DetectGpusAsync();

        gpus.Should().BeEmpty();
    }

    [Fact]
    public async Task DetectGpus_Windows_HandlesMultipleGpus()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return;

        string wmicOutput =
            "Node,AdapterRAM,Name\nPC,8589934592,NVIDIA GeForce RTX 3070\nPC,2147483648,Intel(R) UHD Graphics 770\n";

        _processRunner
            .Setup(p =>
                p.RunAsync(
                    "wmic",
                    It.IsAny<string[]>(),
                    It.IsAny<string?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(new ProcessResult(0, wmicOutput, "", TimeSpan.Zero));

        _capabilities.Setup(c => c.HasEncoder("h264_nvenc")).Returns(true);
        _capabilities.Setup(c => c.HasEncoder("h264_qsv")).Returns(true);

        PlatformHardwareDetector detector = CreateDetector();
        IReadOnlyList<GpuDevice> gpus = await detector.DetectGpusAsync();

        gpus.Should().HaveCount(2);
        gpus[0].Vendor.Should().Be(GpuVendor.Nvidia);
        gpus[1].Vendor.Should().Be(GpuVendor.Intel);
    }

    [Fact]
    public async Task DetectGpus_Windows_WmicFailure_ReturnsEmpty()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return;

        _processRunner
            .Setup(p =>
                p.RunAsync(
                    "wmic",
                    It.IsAny<string[]>(),
                    It.IsAny<string?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(new ProcessResult(1, "", "error", TimeSpan.Zero));

        PlatformHardwareDetector detector = CreateDetector();
        IReadOnlyList<GpuDevice> gpus = await detector.DetectGpusAsync();

        gpus.Should().BeEmpty();
    }

    [Fact]
    public async Task DetectGpus_NoFfmpegEncoders_ReturnsEmpty()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return;

        string wmicOutput = "Node,AdapterRAM,Name\nPC,8589934592,NVIDIA GeForce RTX 3070\n";

        _processRunner
            .Setup(p =>
                p.RunAsync(
                    "wmic",
                    It.IsAny<string[]>(),
                    It.IsAny<string?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(new ProcessResult(0, wmicOutput, "", TimeSpan.Zero));

        _capabilities.Setup(c => c.HasEncoder(It.IsAny<string>())).Returns(false);

        PlatformHardwareDetector detector = CreateDetector();
        IReadOnlyList<GpuDevice> gpus = await detector.DetectGpusAsync();

        gpus.Should().BeEmpty();
    }

    [Fact]
    public async Task DetectGpus_AmdGpu_MaxSessions_Is8()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return;

        string wmicOutput = "Node,AdapterRAM,Name\nPC,8589934592,AMD Radeon RX 7900 XTX\n";

        _processRunner
            .Setup(p =>
                p.RunAsync(
                    "wmic",
                    It.IsAny<string[]>(),
                    It.IsAny<string?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(new ProcessResult(0, wmicOutput, "", TimeSpan.Zero));

        _capabilities.Setup(c => c.HasEncoder("h264_amf")).Returns(true);

        PlatformHardwareDetector detector = CreateDetector();
        IReadOnlyList<GpuDevice> gpus = await detector.DetectGpusAsync();

        gpus.Should().HaveCount(1);
        gpus[0].Vendor.Should().Be(GpuVendor.Amd);
        gpus[0].MaxEncoderSessions.Should().Be(8);
    }

    [Fact]
    public async Task DetectGpus_Mac_ParsesAppleSilicon()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            return;

        string profilerOutput = string.Join(
            "\n",
            "Graphics/Displays:",
            "",
            "    Apple M2 Pro:",
            "",
            "      Chipset Model: Apple M2 Pro",
            "      Type: GPU",
            "      Bus: Built-In",
            "      Total Number of Cores: 19",
            "      Vendor: Apple (0x106b)",
            "      Metal Support: Metal 3",
            "      Displays:",
            "        Color LCD:",
            "          VRAM (Dynamic, Max): 21845 MB",
            ""
        );

        _processRunner
            .Setup(p =>
                p.RunAsync(
                    "system_profiler",
                    It.IsAny<string[]>(),
                    It.IsAny<string?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(new ProcessResult(0, profilerOutput, "", TimeSpan.Zero));

        _capabilities.Setup(c => c.HasEncoder("h264_videotoolbox")).Returns(true);
        _capabilities.Setup(c => c.HasEncoder("hevc_videotoolbox")).Returns(true);

        PlatformHardwareDetector detector = CreateDetector();
        IReadOnlyList<GpuDevice> gpus = await detector.DetectGpusAsync();

        gpus.Should().HaveCount(1);
        gpus[0].Vendor.Should().Be(GpuVendor.Apple);
        gpus[0].Name.Should().Contain("M2 Pro");
        gpus[0].VramMb.Should().Be(21845);
        gpus[0].SupportedCodecs.Should().Contain(VideoCodecType.H264);
        gpus[0].SupportedCodecs.Should().Contain(VideoCodecType.H265);
    }

    [Fact]
    public async Task DetectGpus_Mac_DiscreteAmdGpu()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            return;

        string profilerOutput = string.Join(
            "\n",
            "Graphics/Displays:",
            "",
            "    AMD Radeon Pro 5500M:",
            "",
            "      Chipset Model: AMD Radeon Pro 5500M",
            "      Type: GPU",
            "      VRAM (Total): 8 GB",
            ""
        );

        _processRunner
            .Setup(p =>
                p.RunAsync(
                    "system_profiler",
                    It.IsAny<string[]>(),
                    It.IsAny<string?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(new ProcessResult(0, profilerOutput, "", TimeSpan.Zero));

        _capabilities.Setup(c => c.HasEncoder("h264_amf")).Returns(true);

        PlatformHardwareDetector detector = CreateDetector();
        IReadOnlyList<GpuDevice> gpus = await detector.DetectGpusAsync();

        gpus.Should().HaveCount(1);
        gpus[0].Vendor.Should().Be(GpuVendor.Amd);
        gpus[0].VramMb.Should().Be(8192);
    }

    [Fact]
    public async Task DetectGpus_ProcessException_ReturnsEmpty()
    {
        _processRunner
            .Setup(p =>
                p.RunAsync(
                    It.IsAny<string>(),
                    It.IsAny<string[]>(),
                    It.IsAny<string?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ThrowsAsync(new InvalidOperationException("process not found"));

        PlatformHardwareDetector detector = CreateDetector();
        IReadOnlyList<GpuDevice> gpus = await detector.DetectGpusAsync();

        gpus.Should().BeEmpty();
    }
}
