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
        new(_processRunner.Object, _capabilities.Object, _logger.Object, _storage.Object);

    private void SetupNvidiaSmi(string stdOut, int exitCode = 0)
    {
        _processRunner
            .Setup(p =>
                p.RunAsync(
                    "nvidia-smi",
                    It.IsAny<string[]>(),
                    It.IsAny<string?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(new ProcessResult(exitCode, stdOut, "", TimeSpan.Zero));
    }

    private void SetupLspci(string stdOut, int exitCode = 0)
    {
        _processRunner
            .Setup(p =>
                p.RunAsync(
                    "lspci",
                    It.IsAny<string[]>(),
                    It.IsAny<string?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(new ProcessResult(exitCode, stdOut, "", TimeSpan.Zero));
    }

    [Fact]
    public async Task DetectLinuxGpus_NvidiaSmiReportsGpu_NoDevDri_ReturnsNvenc()
    {
        // The nvidia-container-toolkit Docker case: /dev/nvidia* mounted,
        // /dev/dri never mounted.
        SetupNvidiaSmi("NVIDIA GeForce RTX 4090, 24576, 550.54.15\n");
        _storage.Setup(s => s.Exists("/dev/dri")).Returns(false);
        _storage.Setup(s => s.Exists("/dev/nvidia0")).Returns(true);
        _storage.Setup(s => s.Exists("/dev/nvidiactl")).Returns(true);

        _capabilities.Setup(c => c.HasEncoder("h264_nvenc")).Returns(true);
        _capabilities.Setup(c => c.HasEncoder("hevc_nvenc")).Returns(true);

        PlatformHardwareDetector detector = CreateDetector();
        IReadOnlyList<GpuDevice> gpus = await detector.DetectLinuxGpusAsync(CancellationToken.None);

        gpus.Should().HaveCount(1);
        gpus[0].Vendor.Should().Be(GpuVendor.Nvidia);
        gpus[0].Name.Should().Contain("RTX 4090");
        gpus[0].VramMb.Should().Be(24576);
        gpus[0].DriverVersion.Should().Be("550.54.15");
        gpus[0].SupportedCodecs.Should().Contain(VideoCodecType.H264);
        gpus[0].SupportedCodecs.Should().Contain(VideoCodecType.H265);

        // lspci must never be consulted in the no-/dev/dri branch.
        _processRunner.Verify(
            p =>
                p.RunAsync(
                    "lspci",
                    It.IsAny<string[]>(),
                    It.IsAny<string?>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Never
        );
    }

    [Fact]
    public async Task DetectLinuxGpus_NoGpuSignalsAtAll_ReturnsEmpty()
    {
        SetupNvidiaSmi("", exitCode: 1);
        _storage.Setup(s => s.Exists("/dev/dri")).Returns(false);
        _storage.Setup(s => s.Exists("/dev/nvidia0")).Returns(false);
        _storage.Setup(s => s.Exists("/dev/nvidiactl")).Returns(false);

        PlatformHardwareDetector detector = CreateDetector();
        IReadOnlyList<GpuDevice> gpus = await detector.DetectLinuxGpusAsync(CancellationToken.None);

        gpus.Should().BeEmpty();
    }

    [Fact]
    public async Task DetectLinuxGpus_NvidiaSmiMissing_ThrowsOnRun_ReturnsEmpty()
    {
        _processRunner
            .Setup(p =>
                p.RunAsync(
                    "nvidia-smi",
                    It.IsAny<string[]>(),
                    It.IsAny<string?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ThrowsAsync(new InvalidOperationException("nvidia-smi not found"));
        _storage.Setup(s => s.Exists("/dev/dri")).Returns(false);
        _storage.Setup(s => s.Exists("/dev/nvidia0")).Returns(false);
        _storage.Setup(s => s.Exists("/dev/nvidiactl")).Returns(false);

        PlatformHardwareDetector detector = CreateDetector();
        IReadOnlyList<GpuDevice> gpus = await detector.DetectLinuxGpusAsync(CancellationToken.None);

        gpus.Should().BeEmpty();
    }

    [Fact]
    public async Task DetectLinuxGpus_IntelAmdViaLspci_WithDevDri_Unchanged()
    {
        SetupNvidiaSmi("", exitCode: 1);
        _storage.Setup(s => s.Exists("/dev/dri")).Returns(true);

        string lspciOutput =
            "00:02.0 VGA compatible controller [0300]: Intel Corporation Alder Lake-S GT1 [UHD Graphics 770] [8086:4680]\n";
        SetupLspci(lspciOutput);

        _capabilities.Setup(c => c.HasEncoder("h264_qsv")).Returns(true);

        PlatformHardwareDetector detector = CreateDetector();
        IReadOnlyList<GpuDevice> gpus = await detector.DetectLinuxGpusAsync(CancellationToken.None);

        gpus.Should().HaveCount(1);
        gpus[0].Vendor.Should().Be(GpuVendor.Intel);
        gpus[0].SupportedCodecs.Should().Contain(VideoCodecType.H264);
    }

    [Fact]
    public async Task DetectLinuxGpus_NvidiaViaSmiAndLspci_BareMetal_DoesNotDoubleCount()
    {
        // Bare-metal Linux: /dev/dri present (Nvidia proprietary driver still
        // exposes a render node), nvidia-smi succeeds AND lspci also lists
        // the same card — must be counted once.
        SetupNvidiaSmi("NVIDIA GeForce RTX 3070, 8192, 535.104.05\n");
        _storage.Setup(s => s.Exists("/dev/dri")).Returns(true);

        string lspciOutput =
            "01:00.0 VGA compatible controller [0300]: NVIDIA Corporation GA104 [GeForce RTX 3070] [10de:2484]\n";
        SetupLspci(lspciOutput);

        _capabilities.Setup(c => c.HasEncoder("h264_nvenc")).Returns(true);
        _capabilities.Setup(c => c.HasEncoder("hevc_nvenc")).Returns(true);

        PlatformHardwareDetector detector = CreateDetector();
        IReadOnlyList<GpuDevice> gpus = await detector.DetectLinuxGpusAsync(CancellationToken.None);

        gpus.Should().HaveCount(1);
        gpus[0].Vendor.Should().Be(GpuVendor.Nvidia);
        gpus[0].DriverVersion.Should().Be("535.104.05");
    }

    [Fact]
    public async Task DetectLinuxGpus_NvidiaSmiFails_DeviceNodesPresent_LogsWarningReturnsEmpty()
    {
        SetupNvidiaSmi("", exitCode: 1);
        _storage.Setup(s => s.Exists("/dev/dri")).Returns(false);
        _storage.Setup(s => s.Exists("/dev/nvidia0")).Returns(true);
        _storage.Setup(s => s.Exists("/dev/nvidiactl")).Returns(false);

        PlatformHardwareDetector detector = CreateDetector();
        IReadOnlyList<GpuDevice> gpus = await detector.DetectLinuxGpusAsync(CancellationToken.None);

        gpus.Should().BeEmpty();
    }
}
