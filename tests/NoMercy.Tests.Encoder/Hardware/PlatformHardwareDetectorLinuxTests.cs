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

using Microsoft.Extensions.Logging;
using Moq;
using NoMercy.Encoder.Codecs;
using NoMercy.Encoder.Hardware;
using NoMercy.Encoder.Infrastructure;
using NoMercy.Storage;

namespace NoMercy.Tests.Encoder.Hardware;

/// <summary>
/// Exercises <see cref="PlatformHardwareDetector.DetectLinuxGpusAsync"/> directly
/// (it is <c>internal</c>, exposed to this assembly via <c>InternalsVisibleTo</c>)
/// so the Linux/NVIDIA-without-/dev/dri path can be tested on any host OS,
/// independent of the <see cref="System.Runtime.InteropServices.RuntimeInformation"/>
/// gate in <see cref="PlatformHardwareDetector.DetectGpusAsync"/>.
/// </summary>
public class PlatformHardwareDetectorLinuxTests
{
    private readonly Mock<IProcessRunner> _processRunner = new();
    private readonly Mock<IFfmpegCapabilities> _capabilities = new();
    private readonly Mock<ILogger<PlatformHardwareDetector>> _logger = new();
    private readonly Mock<IStorage> _storage = new();

    private PlatformHardwareDetector CreateDetector() =>
        new(processRunner: _processRunner.Object, ffmpegCapabilities: _capabilities.Object, logger: _logger.Object, storage: _storage.Object);

    private void SetupNvidiaSmi(string stdOut, int exitCode = 0)
    {
        _processRunner
            .Setup(expression: p =>
                p.RunAsync(
                    "nvidia-smi",
                    It.IsAny<string[]>(),
                    It.IsAny<string?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(value: new ProcessResult(ExitCode: exitCode, StdOut: stdOut, StdErr: "", Duration: TimeSpan.Zero));
    }

    private void SetupLspci(string stdOut, int exitCode = 0)
    {
        _processRunner
            .Setup(expression: p =>
                p.RunAsync(
                    "lspci",
                    It.IsAny<string[]>(),
                    It.IsAny<string?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(value: new ProcessResult(ExitCode: exitCode, StdOut: stdOut, StdErr: "", Duration: TimeSpan.Zero));
    }

    [Fact]
    public async Task DetectLinuxGpus_NvidiaSmiReportsGpu_NoDevDri_ReturnsNvenc()
    {
        // The nvidia-container-toolkit Docker case: /dev/nvidia* mounted,
        // /dev/dri never mounted.
        SetupNvidiaSmi(stdOut: "NVIDIA GeForce RTX 4090, 24576, 550.54.15\n");
        _storage.Setup(expression: s => s.Exists("/dev/dri")).Returns(value: false);
        _storage.Setup(expression: s => s.Exists("/dev/nvidia0")).Returns(value: true);
        _storage.Setup(expression: s => s.Exists("/dev/nvidiactl")).Returns(value: true);

        _capabilities.Setup(expression: c => c.HasEncoder("h264_nvenc")).Returns(value: true);
        _capabilities.Setup(expression: c => c.HasEncoder("hevc_nvenc")).Returns(value: true);

        PlatformHardwareDetector detector = CreateDetector();
        IReadOnlyList<GpuDevice> gpus = await detector.DetectLinuxGpusAsync(ct: CancellationToken.None);

        gpus.Should().HaveCount(expected: 1);
        gpus[index: 0].Vendor.Should().Be(expected: GpuVendor.Nvidia);
        gpus[index: 0].Name.Should().Contain(expected: "RTX 4090");
        gpus[index: 0].VramMb.Should().Be(expected: 24576);
        gpus[index: 0].DriverVersion.Should().Be(expected: "550.54.15");
        gpus[index: 0].SupportedCodecs.Should().Contain(expected: VideoCodecType.H264);
        gpus[index: 0].SupportedCodecs.Should().Contain(expected: VideoCodecType.H265);

        // lspci must never be consulted in the no-/dev/dri branch.
        _processRunner.Verify(
            expression: p =>
                p.RunAsync(
                    "lspci",
                    It.IsAny<string[]>(),
                    It.IsAny<string?>(),
                    It.IsAny<CancellationToken>()
                ),
            times: Times.Never
        );
    }

    [Fact]
    public async Task DetectLinuxGpus_NoGpuSignalsAtAll_ReturnsEmpty()
    {
        SetupNvidiaSmi(stdOut: "", exitCode: 1);
        _storage.Setup(expression: s => s.Exists("/dev/dri")).Returns(value: false);
        _storage.Setup(expression: s => s.Exists("/dev/nvidia0")).Returns(value: false);
        _storage.Setup(expression: s => s.Exists("/dev/nvidiactl")).Returns(value: false);

        PlatformHardwareDetector detector = CreateDetector();
        IReadOnlyList<GpuDevice> gpus = await detector.DetectLinuxGpusAsync(ct: CancellationToken.None);

        gpus.Should().BeEmpty();
    }

    [Fact]
    public async Task DetectLinuxGpus_NvidiaSmiMissing_ThrowsOnRun_ReturnsEmpty()
    {
        _processRunner
            .Setup(expression: p =>
                p.RunAsync(
                    "nvidia-smi",
                    It.IsAny<string[]>(),
                    It.IsAny<string?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ThrowsAsync(exception: new InvalidOperationException(message: "nvidia-smi not found"));
        _storage.Setup(expression: s => s.Exists("/dev/dri")).Returns(value: false);
        _storage.Setup(expression: s => s.Exists("/dev/nvidia0")).Returns(value: false);
        _storage.Setup(expression: s => s.Exists("/dev/nvidiactl")).Returns(value: false);

        PlatformHardwareDetector detector = CreateDetector();
        IReadOnlyList<GpuDevice> gpus = await detector.DetectLinuxGpusAsync(ct: CancellationToken.None);

        gpus.Should().BeEmpty();
    }

    [Fact]
    public async Task DetectLinuxGpus_IntelAmdViaLspci_WithDevDri_Unchanged()
    {
        SetupNvidiaSmi(stdOut: "", exitCode: 1);
        _storage.Setup(expression: s => s.Exists("/dev/dri")).Returns(value: true);

        string lspciOutput =
            "00:02.0 VGA compatible controller [0300]: Intel Corporation Alder Lake-S GT1 [UHD Graphics 770] [8086:4680]\n";
        SetupLspci(stdOut: lspciOutput);

        _capabilities.Setup(expression: c => c.HasEncoder("h264_qsv")).Returns(value: true);

        PlatformHardwareDetector detector = CreateDetector();
        IReadOnlyList<GpuDevice> gpus = await detector.DetectLinuxGpusAsync(ct: CancellationToken.None);

        gpus.Should().HaveCount(expected: 1);
        gpus[index: 0].Vendor.Should().Be(expected: GpuVendor.Intel);
        gpus[index: 0].SupportedCodecs.Should().Contain(expected: VideoCodecType.H264);
    }

    [Fact]
    public async Task DetectLinuxGpus_NvidiaViaSmiAndLspci_BareMetal_DoesNotDoubleCount()
    {
        // Bare-metal Linux: /dev/dri present (Nvidia proprietary driver still
        // exposes a render node), nvidia-smi succeeds AND lspci also lists
        // the same card — must be counted once.
        SetupNvidiaSmi(stdOut: "NVIDIA GeForce RTX 3070, 8192, 535.104.05\n");
        _storage.Setup(expression: s => s.Exists("/dev/dri")).Returns(value: true);

        string lspciOutput =
            "01:00.0 VGA compatible controller [0300]: NVIDIA Corporation GA104 [GeForce RTX 3070] [10de:2484]\n";
        SetupLspci(stdOut: lspciOutput);

        _capabilities.Setup(expression: c => c.HasEncoder("h264_nvenc")).Returns(value: true);
        _capabilities.Setup(expression: c => c.HasEncoder("hevc_nvenc")).Returns(value: true);

        PlatformHardwareDetector detector = CreateDetector();
        IReadOnlyList<GpuDevice> gpus = await detector.DetectLinuxGpusAsync(ct: CancellationToken.None);

        gpus.Should().HaveCount(expected: 1);
        gpus[index: 0].Vendor.Should().Be(expected: GpuVendor.Nvidia);
        gpus[index: 0].DriverVersion.Should().Be(expected: "535.104.05");
    }

    [Fact]
    public async Task DetectLinuxGpus_NvidiaSmiFails_DeviceNodesPresent_LogsWarningReturnsEmpty()
    {
        SetupNvidiaSmi(stdOut: "", exitCode: 1);
        _storage.Setup(expression: s => s.Exists("/dev/dri")).Returns(value: false);
        _storage.Setup(expression: s => s.Exists("/dev/nvidia0")).Returns(value: true);
        _storage.Setup(expression: s => s.Exists("/dev/nvidiactl")).Returns(value: false);

        PlatformHardwareDetector detector = CreateDetector();
        IReadOnlyList<GpuDevice> gpus = await detector.DetectLinuxGpusAsync(ct: CancellationToken.None);

        gpus.Should().BeEmpty();
    }
}
