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

using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NoMercy.Encoder.Hardware;
using NoMercy.Encoder.Infrastructure;

namespace NoMercy.Tests.Encoder.Hardware;

/// <summary>
/// AV1 hardware-encode capability gate: a GPU may have the FFmpeg encoder
/// wrapper available (av1_nvenc / av1_amf / av1_qsv / av1_vaapi) without the
/// silicon block. Probing on cards lacking the block fails at codec init and
/// the benchmark gets nothing — these tests pin the pattern-matching gates
/// per vendor so regressions surface up-front.
/// </summary>
public class Av1CapabilityProbeTests
{
    private static Av1CapabilityProbe BuildProbe(IProcessRunner? runner = null) =>
        new(processRunner: runner ?? Mock.Of<IProcessRunner>(), logger: NullLogger.Instance);

    // ── Apple — auto-allowed (silicon presence is the GPU detection itself) ──

    [Fact]
    public async Task SupportsAsync_AppleVendor_DefaultsAllow()
    {
        bool result = await BuildProbe().SupportsAsync(vendor: GpuVendor.Apple, gpuName: "Apple M2 Pro");
        result.Should().BeTrue();
    }

    // ── AMD ───────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(data: "AMD Radeon RX 7900 XTX")]
    [InlineData(data: "AMD Radeon RX 7700 XT")]
    [InlineData(data: "AMD Radeon RX 9070 XT")]
    [InlineData(data: "AMD Radeon Pro W7900")]
    [InlineData(data: "AMD Radeon 8060S Graphics")]
    public async Task SupportsAsync_AmdRdna3Plus_Allowed(string gpuName)
    {
        bool result = await BuildProbe().SupportsAsync(vendor: GpuVendor.Amd, gpuName: gpuName);
        result.Should().BeTrue();
    }

    [Theory]
    [InlineData(data: "AMD Radeon RX 5700 XT")] // RDNA 1
    [InlineData(data: "AMD Radeon RX 6800 XT")] // RDNA 2
    [InlineData(data: "AMD Radeon Pro W5500")] // RDNA 1 workstation
    [InlineData(data: "AMD Radeon Pro W6800")] // RDNA 2 workstation
    [InlineData(data: "AMD Radeon RX Vega 64")] // Vega
    [InlineData(data: "AMD Radeon RX 580")] // Polaris
    public async Task SupportsAsync_AmdPreRdna3_Denied(string gpuName)
    {
        bool result = await BuildProbe().SupportsAsync(vendor: GpuVendor.Amd, gpuName: gpuName);
        result.Should().BeFalse();
    }

    // ── Intel ─────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(data: "Intel Arc A770")] // Alchemist desktop
    [InlineData(data: "Intel Arc A380")] // Alchemist entry-level
    [InlineData(data: "Intel Arc B580")] // Battlemage
    [InlineData(data: "Intel Arc Pro A50")] // Arc Pro workstation
    [InlineData(data: "Intel Arc Graphics")] // Meteor Lake / Lunar Lake integrated
    public async Task SupportsAsync_IntelArc_Allowed(string gpuName)
    {
        bool result = await BuildProbe().SupportsAsync(vendor: GpuVendor.Intel, gpuName: gpuName);
        result.Should().BeTrue();
    }

    [Theory]
    [InlineData(data: "Intel Iris Xe Graphics")] // Tiger Lake iGPU
    [InlineData(data: "Intel UHD Graphics 770")] // Alder Lake iGPU
    [InlineData(data: "Intel HD Graphics 530")] // Skylake iGPU
    public async Task SupportsAsync_IntelPreArc_Denied(string gpuName)
    {
        bool result = await BuildProbe().SupportsAsync(vendor: GpuVendor.Intel, gpuName: gpuName);
        result.Should().BeFalse();
    }

    // ── Nvidia (probes nvidia-smi compute_cap) ────────────────────────────

    [Fact]
    public async Task SupportsAsync_NvidiaAda_FromComputeCap_Allowed()
    {
        // nvidia-smi compute_cap = 8.9 = Ada Lovelace = AV1 NVENC available.
        IProcessRunner runner = BuildRunnerEmitting(stdout: "8.9");
        bool result = await BuildProbe(runner: runner).SupportsAsync(vendor: GpuVendor.Nvidia, gpuName: "RTX 4090");
        result.Should().BeTrue();
    }

    [Fact]
    public async Task SupportsAsync_NvidiaBlackwell_FromComputeCap_Allowed()
    {
        IProcessRunner runner = BuildRunnerEmitting(stdout: "9.0");
        bool result = await BuildProbe(runner: runner).SupportsAsync(vendor: GpuVendor.Nvidia, gpuName: "RTX 5090");
        result.Should().BeTrue();
    }

    [Fact]
    public async Task SupportsAsync_NvidiaAmpere_FromComputeCap_Denied()
    {
        // Ampere = compute_cap 8.6 = no AV1 NVENC silicon.
        IProcessRunner runner = BuildRunnerEmitting(stdout: "8.6");
        bool result = await BuildProbe(runner: runner).SupportsAsync(vendor: GpuVendor.Nvidia, gpuName: "RTX 3090");
        result.Should().BeFalse();
    }

    [Fact]
    public async Task SupportsAsync_NvidiaTuring_FromComputeCap_Denied()
    {
        // Turing = compute_cap 7.5 = no AV1 NVENC silicon.
        IProcessRunner runner = BuildRunnerEmitting(stdout: "7.5");
        bool result = await BuildProbe(runner: runner).SupportsAsync(vendor: GpuVendor.Nvidia, gpuName: "RTX 2080");
        result.Should().BeFalse();
    }

    [Fact]
    public async Task SupportsAsync_NvidiaMixedGenerations_OneAdaInTheMix_Allowed()
    {
        // Multi-GPU host: one Ada Lovelace card is enough to enable AV1.
        IProcessRunner runner = BuildRunnerEmitting(stdout: "7.5\n8.9");
        bool result = await BuildProbe(runner: runner).SupportsAsync(vendor: GpuVendor.Nvidia, gpuName: "RTX 4090");
        result.Should().BeTrue();
    }

    [Fact]
    public async Task SupportsAsync_NvidiaSmiMissing_DefaultsAllow()
    {
        // nvidia-smi unavailable (exit != 0): default to allow so the probe-
        // and-log fallback at benchmark time gets a chance to surface failure.
        IProcessRunner runner = BuildRunner(exitCode: 127, stdout: "");
        bool result = await BuildProbe(runner: runner).SupportsAsync(vendor: GpuVendor.Nvidia, gpuName: "GeForce");
        result.Should().BeTrue();
    }

    [Fact]
    public async Task SupportsAsync_NvidiaSmiEmptyOutput_DefaultsAllow()
    {
        IProcessRunner runner = BuildRunner(exitCode: 0, stdout: "");
        bool result = await BuildProbe(runner: runner).SupportsAsync(vendor: GpuVendor.Nvidia, gpuName: "GeForce");
        result.Should().BeTrue();
    }

    [Fact]
    public async Task SupportsAsync_NvidiaSmiThrows_DefaultsAllow()
    {
        Mock<IProcessRunner> mock = new();
        mock.Setup(expression: r =>
                r.RunAsync(
                    It.IsAny<string>(),
                    It.IsAny<string[]>(),
                    It.IsAny<string?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ThrowsAsync(exception: new InvalidOperationException(message: "nvidia-smi not on PATH"));
        bool result = await BuildProbe(runner: mock.Object).SupportsAsync(vendor: GpuVendor.Nvidia, gpuName: "GeForce");
        result.Should().BeTrue();
    }

    private static IProcessRunner BuildRunnerEmitting(string stdout) =>
        BuildRunner(exitCode: 0, stdout: stdout);

    private static IProcessRunner BuildRunner(int exitCode, string stdout)
    {
        ProcessResult result = new(ExitCode: exitCode, StdOut: stdout, StdErr: "", Duration: TimeSpan.Zero);
        Mock<IProcessRunner> mock = new();
        mock.Setup(expression: r =>
                r.RunAsync(
                    It.IsAny<string>(),
                    It.IsAny<string[]>(),
                    It.IsAny<string?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(value: result);
        return mock.Object;
    }
}
