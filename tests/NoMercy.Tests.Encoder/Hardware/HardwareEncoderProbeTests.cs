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
        new(processRunner: processRunner.Object, logger: NullLogger<HardwareEncoderProbe>.Instance);

    private static Mock<IProcessRunner> BuildRunnerReturning(int exitCode) =>
        BuildRunnerReturning(exitCode: exitCode, capturedArgs: null);

    private static Mock<IProcessRunner> BuildRunnerReturning(
        int exitCode,
        List<string[]>? capturedArgs
    )
    {
        Mock<IProcessRunner> runner = new();
        runner
            .Setup(expression: r =>
                r.RunAsync(
                    It.IsAny<string>(),
                    It.IsAny<string[]>(),
                    It.IsAny<string?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Callback<string, string[], string?, CancellationToken>(
                action: (_, args, _, _) => capturedArgs?.Add(item: args)
            )
            .ReturnsAsync(
                value: new ProcessResult(ExitCode: exitCode, StdOut: "", StdErr: exitCode == 0 ? "" : "init failed", Duration: TimeSpan.Zero)
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
        HardwareEncoderProbe probe = BuildProbe(processRunner: runner);

        IReadOnlySet<string> usable = await probe.ProbeAsync(candidateHardwareEncoders: ["h264_nvenc"]);

        usable.Should().Contain(expected: "h264_nvenc");
    }

    [Fact]
    public async Task ProbeAsync_MarksEncoderUnusable_WhenInitExitsNonzero()
    {
        Mock<IProcessRunner> runner = BuildRunnerReturning(exitCode: 1);
        HardwareEncoderProbe probe = BuildProbe(processRunner: runner);

        // This is exactly Fillz's field shape: h264_amf is in ffmpeg's
        // compiled encoder list, but the real init fails on this host.
        IReadOnlySet<string> usable = await probe.ProbeAsync(candidateHardwareEncoders: ["h264_amf"]);

        usable.Should().NotContain(unexpected: "h264_amf");
        usable.Should().BeEmpty();
    }

    [Fact]
    public async Task ProbeAsync_MixedCandidates_KeepsOnlyThoseThatInitialize()
    {
        Mock<IProcessRunner> runner = new();
        runner
            .Setup(expression: r =>
                r.RunAsync(
                    It.IsAny<string>(),
                    It.Is<string[]>(a => a.Contains("h264_nvenc")),
                    It.IsAny<string?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(value: new ProcessResult(ExitCode: 0, StdOut: "", StdErr: "", Duration: TimeSpan.Zero));
        runner
            .Setup(expression: r =>
                r.RunAsync(
                    It.IsAny<string>(),
                    It.Is<string[]>(a => a.Contains("h264_amf")),
                    It.IsAny<string?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(value: new ProcessResult(ExitCode: 1, StdOut: "", StdErr: "no AMD device", Duration: TimeSpan.Zero));

        HardwareEncoderProbe probe = BuildProbe(processRunner: runner);

        IReadOnlySet<string> usable = await probe.ProbeAsync(candidateHardwareEncoders: ["h264_nvenc", "h264_amf"]);

        usable.Should().Contain(expected: "h264_nvenc");
        usable.Should().NotContain(unexpected: "h264_amf");
    }

    [Fact]
    public async Task ProbeAsync_ProcessThrows_TreatsEncoderAsUnusable()
    {
        Mock<IProcessRunner> runner = new();
        runner
            .Setup(expression: r =>
                r.RunAsync(
                    It.IsAny<string>(),
                    It.IsAny<string[]>(),
                    It.IsAny<string?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ThrowsAsync(exception: new InvalidOperationException(message: "ffmpeg binary missing"));

        HardwareEncoderProbe probe = BuildProbe(processRunner: runner);

        IReadOnlySet<string> usable = await probe.ProbeAsync(candidateHardwareEncoders: ["h264_qsv"]);

        usable.Should().BeEmpty();
    }

    [Fact]
    public async Task ProbeAsync_UnknownEncoderFamily_TreatsAsUnusableRatherThanGuessing()
    {
        Mock<IProcessRunner> runner = BuildRunnerReturning(exitCode: 0);
        HardwareEncoderProbe probe = BuildProbe(processRunner: runner);

        // A name outside every known hardware family (NVENC/AMF/QSV/VAAPI/
        // VideoToolbox) has no known-working invocation — never guess one.
        IReadOnlySet<string> usable = await probe.ProbeAsync(candidateHardwareEncoders: ["some_future_hw_encoder"]);

        usable.Should().BeEmpty();
        runner.Verify(
            expression: r =>
                r.RunAsync(
                    It.IsAny<string>(),
                    It.IsAny<string[]>(),
                    It.IsAny<string?>(),
                    It.IsAny<CancellationToken>()
                ),
            times: Times.Never
        );
    }

    [Fact]
    public async Task ProbeAsync_CallerCancellation_Propagates()
    {
        Mock<IProcessRunner> runner = new();
        runner
            .Setup(expression: r =>
                r.RunAsync(
                    It.IsAny<string>(),
                    It.IsAny<string[]>(),
                    It.IsAny<string?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ThrowsAsync(exception: new OperationCanceledException());

        HardwareEncoderProbe probe = BuildProbe(processRunner: runner);

        using CancellationTokenSource cts = new();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(testCode: () =>
            probe.ProbeAsync(candidateHardwareEncoders: ["h264_nvenc"], ct: cts.Token)
        );
    }

    [Fact]
    public async Task ProbeAsync_EmptyCandidates_ReturnsEmptySet()
    {
        Mock<IProcessRunner> runner = BuildRunnerReturning(exitCode: 0);
        HardwareEncoderProbe probe = BuildProbe(processRunner: runner);

        IReadOnlySet<string> usable = await probe.ProbeAsync(candidateHardwareEncoders: []);

        usable.Should().BeEmpty();
    }

    [Fact]
    public async Task ProbeAsync_DeduplicatesCaseInsensitiveCandidates()
    {
        Mock<IProcessRunner> runner = BuildRunnerReturning(exitCode: 0);
        HardwareEncoderProbe probe = BuildProbe(processRunner: runner);

        IReadOnlySet<string> usable = await probe.ProbeAsync(candidateHardwareEncoders: ["h264_nvenc", "H264_NVENC"]);

        usable.Should().ContainSingle();
    }

    // -------------------------------------------------------------------------
    // Per-family invocation shape — the crux the task calls out. Locks down
    // the exact minimal working invocation per hardware encoder family so a
    // future edit that breaks one is caught here instead of on a user's box.
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData(data: "h264_nvenc")]
    [InlineData(data: "hevc_nvenc")]
    [InlineData(data: "av1_nvenc")]
    [InlineData(data: "h264_amf")]
    [InlineData(data: "hevc_amf")]
    [InlineData(data: "av1_amf")]
    [InlineData(data: "h264_videotoolbox")]
    [InlineData(data: "hevc_videotoolbox")]
    public async Task ProbeAsync_NvencAmfVideotoolbox_UsesDirectSoftwareFrameInvocation(
        string encoderName
    )
    {
        List<string[]> captured = [];
        Mock<IProcessRunner> runner = BuildRunnerReturning(exitCode: 0, capturedArgs: captured);
        HardwareEncoderProbe probe = BuildProbe(processRunner: runner);

        await probe.ProbeAsync(candidateHardwareEncoders: [encoderName]);

        string[] args = Assert.Single(collection: captured);
        args.Should().Contain(expected: ["-f", "lavfi", "-i", "nullsrc=s=256x256", "-c:v", encoderName]);
        // No hardware device / upload filter chain — NVENC, AMF and
        // VideoToolbox perform the upload internally.
        args.Should().NotContain(unexpected: "-init_hw_device");
        args.Should().NotContain(unexpected: "-vaapi_device");
        args.Should().NotContain(unexpected: "-vf");
    }

    [Theory]
    [InlineData(data: "h264_qsv")]
    [InlineData(data: "hevc_qsv")]
    [InlineData(data: "av1_qsv")]
    public async Task ProbeAsync_Qsv_UsesHwDeviceAndUploadFilterChain(string encoderName)
    {
        List<string[]> captured = [];
        Mock<IProcessRunner> runner = BuildRunnerReturning(exitCode: 0, capturedArgs: captured);
        HardwareEncoderProbe probe = BuildProbe(processRunner: runner);

        await probe.ProbeAsync(candidateHardwareEncoders: [encoderName]);

        string[] args = Assert.Single(collection: captured);
        args.Should().Contain(expected: ["-init_hw_device", "qsv=hw"]);
        args.Should().Contain(predicate: a => a.Contains("hwupload"));
        args.Should().Contain(expected: ["-c:v", encoderName]);
    }

    [Theory]
    [InlineData(data: "h264_vaapi")]
    [InlineData(data: "hevc_vaapi")]
    [InlineData(data: "av1_vaapi")]
    public async Task ProbeAsync_Vaapi_UsesRenderDeviceAndUploadFilterChain(string encoderName)
    {
        List<string[]> captured = [];
        Mock<IProcessRunner> runner = BuildRunnerReturning(exitCode: 0, capturedArgs: captured);
        HardwareEncoderProbe probe = BuildProbe(processRunner: runner);

        await probe.ProbeAsync(candidateHardwareEncoders: [encoderName]);

        string[] args = Assert.Single(collection: captured);
        args.Should().Contain(expected: ["-vaapi_device", "/dev/dri/renderD128"]);
        args.Should().Contain(predicate: a => a.Contains("hwupload"));
        args.Should().Contain(expected: ["-c:v", encoderName]);
    }
}
