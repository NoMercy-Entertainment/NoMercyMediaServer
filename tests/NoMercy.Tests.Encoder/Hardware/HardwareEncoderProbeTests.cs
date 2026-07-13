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
/// Pins <see cref="HardwareEncoderProbe"/> — the authoritative "does this
/// hardware encoder actually initialize" signal that supersedes both
/// ffmpeg's compiled-in encoder list and GPU-vendor detection for selection
/// purposes. See <c>PlanStage.IsHardwareEncoderSelectable</c> for the
/// consumer.
/// </summary>
public class HardwareEncoderProbeTests
{
    private static HardwareEncoderProbe BuildProbe(Mock<IProcessRunner> processRunner) =>
        new(processRunner.Object, NullLogger<HardwareEncoderProbe>.Instance);

    private static Mock<IProcessRunner> BuildRunnerReturning(int exitCode) =>
        BuildRunnerReturning(exitCode, capturedArgs: null);

    private static Mock<IProcessRunner> BuildRunnerReturning(
        int exitCode,
        List<string[]>? capturedArgs
    )
    {
        Mock<IProcessRunner> runner = new();
        runner
            .Setup(r =>
                r.RunAsync(
                    It.IsAny<string>(),
                    It.IsAny<string[]>(),
                    It.IsAny<string?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Callback<string, string[], string?, CancellationToken>(
                (_, args, _, _) => capturedArgs?.Add(args)
            )
            .ReturnsAsync(
                new ProcessResult(exitCode, "", exitCode == 0 ? "" : "init failed", TimeSpan.Zero)
            );
        return runner;
    }

    // -------------------------------------------------------------------------
    // (a) exit code is the authority
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ProbeAsync_MarksEncoderUsable_WhenInitExitsZero()
    {
        Mock<IProcessRunner> runner = BuildRunnerReturning(exitCode: 0);
        HardwareEncoderProbe probe = BuildProbe(runner);

        IReadOnlySet<string> usable = await probe.ProbeAsync(["h264_nvenc"]);

        usable.Should().Contain("h264_nvenc");
    }

    [Fact]
    public async Task ProbeAsync_MarksEncoderUnusable_WhenInitExitsNonzero()
    {
        Mock<IProcessRunner> runner = BuildRunnerReturning(exitCode: 1);
        HardwareEncoderProbe probe = BuildProbe(runner);

        // This is exactly Fillz's field shape: h264_amf is in ffmpeg's
        // compiled encoder list, but the real init fails on this host.
        IReadOnlySet<string> usable = await probe.ProbeAsync(["h264_amf"]);

        usable.Should().NotContain("h264_amf");
        usable.Should().BeEmpty();
    }

    [Fact]
    public async Task ProbeAsync_MixedCandidates_KeepsOnlyThoseThatInitialize()
    {
        Mock<IProcessRunner> runner = new();
        runner
            .Setup(r =>
                r.RunAsync(
                    It.IsAny<string>(),
                    It.Is<string[]>(a => a.Contains("h264_nvenc")),
                    It.IsAny<string?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(new ProcessResult(0, "", "", TimeSpan.Zero));
        runner
            .Setup(r =>
                r.RunAsync(
                    It.IsAny<string>(),
                    It.Is<string[]>(a => a.Contains("h264_amf")),
                    It.IsAny<string?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(new ProcessResult(1, "", "no AMD device", TimeSpan.Zero));

        HardwareEncoderProbe probe = BuildProbe(runner);

        IReadOnlySet<string> usable = await probe.ProbeAsync(["h264_nvenc", "h264_amf"]);

        usable.Should().Contain("h264_nvenc");
        usable.Should().NotContain("h264_amf");
    }

    [Fact]
    public async Task ProbeAsync_ProcessThrows_TreatsEncoderAsUnusable()
    {
        Mock<IProcessRunner> runner = new();
        runner
            .Setup(r =>
                r.RunAsync(
                    It.IsAny<string>(),
                    It.IsAny<string[]>(),
                    It.IsAny<string?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ThrowsAsync(new InvalidOperationException("ffmpeg binary missing"));

        HardwareEncoderProbe probe = BuildProbe(runner);

        IReadOnlySet<string> usable = await probe.ProbeAsync(["h264_qsv"]);

        usable.Should().BeEmpty();
    }

    [Fact]
    public async Task ProbeAsync_UnknownEncoderFamily_TreatsAsUnusableRatherThanGuessing()
    {
        Mock<IProcessRunner> runner = BuildRunnerReturning(exitCode: 0);
        HardwareEncoderProbe probe = BuildProbe(runner);

        // A name outside every known hardware family (NVENC/AMF/QSV/VAAPI/
        // VideoToolbox) has no known-working invocation — never guess one.
        IReadOnlySet<string> usable = await probe.ProbeAsync(["some_future_hw_encoder"]);

        usable.Should().BeEmpty();
        runner.Verify(
            r =>
                r.RunAsync(
                    It.IsAny<string>(),
                    It.IsAny<string[]>(),
                    It.IsAny<string?>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Never
        );
    }

    [Fact]
    public async Task ProbeAsync_CallerCancellation_Propagates()
    {
        Mock<IProcessRunner> runner = new();
        runner
            .Setup(r =>
                r.RunAsync(
                    It.IsAny<string>(),
                    It.IsAny<string[]>(),
                    It.IsAny<string?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ThrowsAsync(new OperationCanceledException());

        HardwareEncoderProbe probe = BuildProbe(runner);

        using CancellationTokenSource cts = new();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            probe.ProbeAsync(["h264_nvenc"], cts.Token)
        );
    }

    [Fact]
    public async Task ProbeAsync_EmptyCandidates_ReturnsEmptySet()
    {
        Mock<IProcessRunner> runner = BuildRunnerReturning(exitCode: 0);
        HardwareEncoderProbe probe = BuildProbe(runner);

        IReadOnlySet<string> usable = await probe.ProbeAsync([]);

        usable.Should().BeEmpty();
    }

    [Fact]
    public async Task ProbeAsync_DeduplicatesCaseInsensitiveCandidates()
    {
        Mock<IProcessRunner> runner = BuildRunnerReturning(exitCode: 0);
        HardwareEncoderProbe probe = BuildProbe(runner);

        IReadOnlySet<string> usable = await probe.ProbeAsync(["h264_nvenc", "H264_NVENC"]);

        usable.Should().ContainSingle();
    }

    // -------------------------------------------------------------------------
    // Per-family invocation shape — the crux the task calls out. Locks down
    // the exact minimal working invocation per hardware encoder family so a
    // future edit that breaks one is caught here instead of on a user's box.
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData("h264_nvenc")]
    [InlineData("hevc_nvenc")]
    [InlineData("av1_nvenc")]
    [InlineData("h264_amf")]
    [InlineData("hevc_amf")]
    [InlineData("av1_amf")]
    [InlineData("h264_videotoolbox")]
    [InlineData("hevc_videotoolbox")]
    public async Task ProbeAsync_NvencAmfVideotoolbox_UsesDirectSoftwareFrameInvocation(
        string encoderName
    )
    {
        List<string[]> captured = [];
        Mock<IProcessRunner> runner = BuildRunnerReturning(exitCode: 0, captured);
        HardwareEncoderProbe probe = BuildProbe(runner);

        await probe.ProbeAsync([encoderName]);

        string[] args = Assert.Single(captured);
        args.Should().Contain(["-f", "lavfi", "-i", "nullsrc=s=256x256", "-c:v", encoderName]);
        // No hardware device / upload filter chain — NVENC, AMF and
        // VideoToolbox perform the upload internally.
        args.Should().NotContain("-init_hw_device");
        args.Should().NotContain("-vaapi_device");
        args.Should().NotContain("-vf");
    }

    [Theory]
    [InlineData("h264_qsv")]
    [InlineData("hevc_qsv")]
    [InlineData("av1_qsv")]
    public async Task ProbeAsync_Qsv_UsesHwDeviceAndUploadFilterChain(string encoderName)
    {
        List<string[]> captured = [];
        Mock<IProcessRunner> runner = BuildRunnerReturning(exitCode: 0, captured);
        HardwareEncoderProbe probe = BuildProbe(runner);

        await probe.ProbeAsync([encoderName]);

        string[] args = Assert.Single(captured);
        args.Should().Contain(["-init_hw_device", "qsv=hw"]);
        args.Should().Contain(a => a.Contains("hwupload"));
        args.Should().Contain(["-c:v", encoderName]);
    }

    [Theory]
    [InlineData("h264_vaapi")]
    [InlineData("hevc_vaapi")]
    [InlineData("av1_vaapi")]
    public async Task ProbeAsync_Vaapi_UsesRenderDeviceAndUploadFilterChain(string encoderName)
    {
        List<string[]> captured = [];
        Mock<IProcessRunner> runner = BuildRunnerReturning(exitCode: 0, captured);
        HardwareEncoderProbe probe = BuildProbe(runner);

        await probe.ProbeAsync([encoderName]);

        string[] args = Assert.Single(captured);
        args.Should().Contain(["-vaapi_device", "/dev/dri/renderD128"]);
        args.Should().Contain(a => a.Contains("hwupload"));
        args.Should().Contain(["-c:v", encoderName]);
    }
}
