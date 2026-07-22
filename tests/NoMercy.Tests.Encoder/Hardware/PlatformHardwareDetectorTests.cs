// -----------------------------------------------------------------------------
//  Copyright (c) 2024-present NoMercy Entertainment. All rights reserved.
//
//  This file is part of NoMercy MediaServer, source-available software (NOT open
//  source). Personal use and contributions are welcome; distribution, resale,
//  relicensing, and commercial exploitation are prohibited without explicit
//  written consent. See LICENSE for full terms. Distributed WITHOUT ANY WARRANTY.
//
//  SPDX-License-Identifier: LicenseRef-NoMercy-Proprietary
// -----------------------------------------------------------------------------

using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using Moq;
using NoMercy.Encoder.Codecs;
using NoMercy.Encoder.Hardware;
using NoMercy.Encoder.Infrastructure;
using NoMercy.Tests.Encoder.Storage;

namespace NoMercy.Tests.Encoder.Hardware;

public class PlatformHardwareDetectorTests
{
    private readonly Mock<IProcessRunner> _processRunner = new();
    private readonly Mock<IFfmpegCapabilities> _capabilities = new();
    private readonly Mock<ILogger<PlatformHardwareDetector>> _logger = new();

    private PlatformHardwareDetector CreateDetector() =>
        new(
            processRunner: _processRunner.Object,
            ffmpegCapabilities: _capabilities.Object,
            logger: _logger.Object,
            storage: TestStorageFactory.CreateLocal()
        );

    [Fact]
    public async Task DetectCpuCoreCount_ReturnsProcessorCount()
    {
        PlatformHardwareDetector detector = CreateDetector();
        int cores = await detector.DetectCpuCoreCountAsync();
        cores.Should().Be(expected: Environment.ProcessorCount);
    }

    [Fact]
    public async Task DetectGpus_Windows_ParsesWmicOutput()
    {
        if (!RuntimeInformation.IsOSPlatform(osPlatform: OSPlatform.Windows))
            return;

        string wmicOutput = "Node,AdapterRAM,Name\nPC,8589934592,NVIDIA GeForce RTX 3070\n";

        _processRunner
            .Setup(expression: p =>
                p.RunAsync(
                    "wmic",
                    It.IsAny<string[]>(),
                    It.IsAny<string?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(value: new ProcessResult(ExitCode: 0, StdOut: wmicOutput, StdErr: "", Duration: TimeSpan.Zero));

        _capabilities.Setup(expression: c => c.HasEncoder("h264_nvenc")).Returns(value: true);
        _capabilities.Setup(expression: c => c.HasEncoder("hevc_nvenc")).Returns(value: true);

        PlatformHardwareDetector detector = CreateDetector();
        IReadOnlyList<GpuDevice> gpus = await detector.DetectGpusAsync();

        gpus.Should().HaveCount(expected: 1);
        gpus[index: 0].Vendor.Should().Be(expected: GpuVendor.Nvidia);
        gpus[index: 0].Name.Should().Contain(expected: "RTX 3070");
        gpus[index: 0].VramMb.Should().Be(expected: 8192);
        gpus[index: 0].MaxEncoderSessions.Should().Be(expected: 8);
        gpus[index: 0].SupportedCodecs.Should().Contain(expected: VideoCodecType.H264);
        gpus[index: 0].SupportedCodecs.Should().Contain(expected: VideoCodecType.H265);
    }

    [Fact]
    public async Task DetectGpus_Windows_SkipsNonGpuAdapters()
    {
        if (!RuntimeInformation.IsOSPlatform(osPlatform: OSPlatform.Windows))
            return;

        string wmicOutput = "Node,AdapterRAM,Name\nPC,0,Microsoft Basic Display Adapter\n";

        _processRunner
            .Setup(expression: p =>
                p.RunAsync(
                    "wmic",
                    It.IsAny<string[]>(),
                    It.IsAny<string?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(value: new ProcessResult(ExitCode: 0, StdOut: wmicOutput, StdErr: "", Duration: TimeSpan.Zero));

        PlatformHardwareDetector detector = CreateDetector();
        IReadOnlyList<GpuDevice> gpus = await detector.DetectGpusAsync();

        gpus.Should().BeEmpty();
    }

    [Fact]
    public async Task DetectGpus_Windows_HandlesMultipleGpus()
    {
        if (!RuntimeInformation.IsOSPlatform(osPlatform: OSPlatform.Windows))
            return;

        string wmicOutput =
            "Node,AdapterRAM,Name\nPC,8589934592,NVIDIA GeForce RTX 3070\nPC,2147483648,Intel(R) UHD Graphics 770\n";

        _processRunner
            .Setup(expression: p =>
                p.RunAsync(
                    "wmic",
                    It.IsAny<string[]>(),
                    It.IsAny<string?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(value: new ProcessResult(ExitCode: 0, StdOut: wmicOutput, StdErr: "", Duration: TimeSpan.Zero));

        _capabilities.Setup(expression: c => c.HasEncoder("h264_nvenc")).Returns(value: true);
        _capabilities.Setup(expression: c => c.HasEncoder("h264_qsv")).Returns(value: true);

        PlatformHardwareDetector detector = CreateDetector();
        IReadOnlyList<GpuDevice> gpus = await detector.DetectGpusAsync();

        gpus.Should().HaveCount(expected: 2);
        gpus[index: 0].Vendor.Should().Be(expected: GpuVendor.Nvidia);
        gpus[index: 1].Vendor.Should().Be(expected: GpuVendor.Intel);
    }

    [Fact]
    public async Task DetectGpus_Windows_WmicFailure_ReturnsEmpty()
    {
        if (!RuntimeInformation.IsOSPlatform(osPlatform: OSPlatform.Windows))
            return;

        _processRunner
            .Setup(expression: p =>
                p.RunAsync(
                    "wmic",
                    It.IsAny<string[]>(),
                    It.IsAny<string?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(value: new ProcessResult(ExitCode: 1, StdOut: "", StdErr: "error", Duration: TimeSpan.Zero));

        PlatformHardwareDetector detector = CreateDetector();
        IReadOnlyList<GpuDevice> gpus = await detector.DetectGpusAsync();

        gpus.Should().BeEmpty();
    }

    [Fact]
    public async Task DetectGpus_Windows_WmicLaunchThrows_FallsBackToPowerShell()
    {
        if (!RuntimeInformation.IsOSPlatform(osPlatform: OSPlatform.Windows))
            return;

        // Reproduces the Windows 11 24H2+ case where wmic.exe has been
        // removed: Process.Start throws Win32Exception(2) before a
        // ProcessResult is ever produced, instead of the runner returning a
        // non-zero exit code.
        _processRunner
            .Setup(expression: p =>
                p.RunAsync(
                    "wmic",
                    It.IsAny<string[]>(),
                    It.IsAny<string?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ThrowsAsync(exception: new Win32Exception(error: 2, message: "The system cannot find the file specified"));

        string powerShellOutput = "NVIDIA GeForce GTX 1060|6442450944|31.0.15.3667\n";

        _processRunner
            .Setup(expression: p =>
                p.RunAsync(
                    "powershell",
                    It.IsAny<string[]>(),
                    It.IsAny<string?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(value: new ProcessResult(ExitCode: 0, StdOut: powerShellOutput, StdErr: "", Duration: TimeSpan.Zero));

        _capabilities.Setup(expression: c => c.HasEncoder("h264_nvenc")).Returns(value: true);
        _capabilities.Setup(expression: c => c.HasEncoder("hevc_nvenc")).Returns(value: true);

        PlatformHardwareDetector detector = CreateDetector();
        IReadOnlyList<GpuDevice> gpus = await detector.DetectGpusAsync();

        gpus.Should().HaveCount(expected: 1);
        gpus[index: 0].Vendor.Should().Be(expected: GpuVendor.Nvidia);
        gpus[index: 0].Name.Should().Contain(expected: "GTX 1060");
        gpus[index: 0].VramMb.Should().Be(expected: 6144);
    }

    [Fact]
    public async Task DetectGpus_NoFfmpegEncoders_ReturnsEmpty()
    {
        if (!RuntimeInformation.IsOSPlatform(osPlatform: OSPlatform.Windows))
            return;

        string wmicOutput = "Node,AdapterRAM,Name\nPC,8589934592,NVIDIA GeForce RTX 3070\n";

        _processRunner
            .Setup(expression: p =>
                p.RunAsync(
                    "wmic",
                    It.IsAny<string[]>(),
                    It.IsAny<string?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(value: new ProcessResult(ExitCode: 0, StdOut: wmicOutput, StdErr: "", Duration: TimeSpan.Zero));

        _capabilities.Setup(expression: c => c.HasEncoder(It.IsAny<string>())).Returns(value: false);

        PlatformHardwareDetector detector = CreateDetector();
        IReadOnlyList<GpuDevice> gpus = await detector.DetectGpusAsync();

        gpus.Should().BeEmpty();
    }

    [Fact]
    public async Task DetectGpus_AmdGpu_MaxSessions_Is8()
    {
        if (!RuntimeInformation.IsOSPlatform(osPlatform: OSPlatform.Windows))
            return;

        string wmicOutput = "Node,AdapterRAM,Name\nPC,8589934592,AMD Radeon RX 7900 XTX\n";

        _processRunner
            .Setup(expression: p =>
                p.RunAsync(
                    "wmic",
                    It.IsAny<string[]>(),
                    It.IsAny<string?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(value: new ProcessResult(ExitCode: 0, StdOut: wmicOutput, StdErr: "", Duration: TimeSpan.Zero));

        _capabilities.Setup(expression: c => c.HasEncoder("h264_amf")).Returns(value: true);

        PlatformHardwareDetector detector = CreateDetector();
        IReadOnlyList<GpuDevice> gpus = await detector.DetectGpusAsync();

        gpus.Should().HaveCount(expected: 1);
        gpus[index: 0].Vendor.Should().Be(expected: GpuVendor.Amd);
        gpus[index: 0].MaxEncoderSessions.Should().Be(expected: 8);
    }

    [Fact]
    public async Task DetectGpus_Mac_ParsesAppleSilicon()
    {
        if (!RuntimeInformation.IsOSPlatform(osPlatform: OSPlatform.OSX))
            return;

        string profilerOutput = string.Join(
            separator: "\n", value: ["Graphics/Displays:", "", "    Apple M2 Pro:", "", "      Chipset Model: Apple M2 Pro", "      Type: GPU", "      Bus: Built-In", "      Total Number of Cores: 19", "      Vendor: Apple (0x106b)", "      Metal Support: Metal 3", "      Displays:", "        Color LCD:", "          VRAM (Dynamic, Max): 21845 MB", ""]
        );

        _processRunner
            .Setup(expression: p =>
                p.RunAsync(
                    "system_profiler",
                    It.IsAny<string[]>(),
                    It.IsAny<string?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(value: new ProcessResult(ExitCode: 0, StdOut: profilerOutput, StdErr: "", Duration: TimeSpan.Zero));

        _capabilities.Setup(expression: c => c.HasEncoder("h264_videotoolbox")).Returns(value: true);
        _capabilities.Setup(expression: c => c.HasEncoder("hevc_videotoolbox")).Returns(value: true);

        PlatformHardwareDetector detector = CreateDetector();
        IReadOnlyList<GpuDevice> gpus = await detector.DetectGpusAsync();

        gpus.Should().HaveCount(expected: 1);
        gpus[index: 0].Vendor.Should().Be(expected: GpuVendor.Apple);
        gpus[index: 0].Name.Should().Contain(expected: "M2 Pro");
        gpus[index: 0].VramMb.Should().Be(expected: 21845);
        gpus[index: 0].SupportedCodecs.Should().Contain(expected: VideoCodecType.H264);
        gpus[index: 0].SupportedCodecs.Should().Contain(expected: VideoCodecType.H265);
    }

    [Fact]
    public async Task DetectGpus_Mac_DiscreteAmdGpu()
    {
        if (!RuntimeInformation.IsOSPlatform(osPlatform: OSPlatform.OSX))
            return;

        string profilerOutput = string.Join(
            separator: "\n", value: ["Graphics/Displays:", "", "    AMD Radeon Pro 5500M:", "", "      Chipset Model: AMD Radeon Pro 5500M", "      Type: GPU", "      VRAM (Total): 8 GB", ""]
        );

        _processRunner
            .Setup(expression: p =>
                p.RunAsync(
                    "system_profiler",
                    It.IsAny<string[]>(),
                    It.IsAny<string?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(value: new ProcessResult(ExitCode: 0, StdOut: profilerOutput, StdErr: "", Duration: TimeSpan.Zero));

        _capabilities.Setup(expression: c => c.HasEncoder("h264_amf")).Returns(value: true);

        PlatformHardwareDetector detector = CreateDetector();
        IReadOnlyList<GpuDevice> gpus = await detector.DetectGpusAsync();

        gpus.Should().HaveCount(expected: 1);
        gpus[index: 0].Vendor.Should().Be(expected: GpuVendor.Amd);
        gpus[index: 0].VramMb.Should().Be(expected: 8192);
    }

    [Fact]
    public async Task DetectGpus_ProcessException_ReturnsEmpty()
    {
        _processRunner
            .Setup(expression: p =>
                p.RunAsync(
                    It.IsAny<string>(),
                    It.IsAny<string[]>(),
                    It.IsAny<string?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ThrowsAsync(exception: new InvalidOperationException(message: "process not found"));

        PlatformHardwareDetector detector = CreateDetector();
        IReadOnlyList<GpuDevice> gpus = await detector.DetectGpusAsync();

        gpus.Should().BeEmpty();
    }
}
