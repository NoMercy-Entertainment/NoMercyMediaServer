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
using NoMercy.Encoder.Codecs;
using NoMercy.Encoder.Composition;
using NoMercy.Encoder.ContentAnalysis;
using NoMercy.Encoder.Infrastructure;
using NoMercy.Storage;

namespace NoMercy.Tests.Encoder.ContentAnalysis;

/// <summary>
/// The probe exists to answer whether re-encoding shrinks a file, so it has to
/// measure the encoder that will actually run. A hardware encoder asked for the
/// same quality number spends materially more bitrate than the software one —
/// measured on a real 1080p source, x265 at CRF 20 produced 2118 kbps where
/// NVENC at CQ 20 produced 4618. Measuring the wrong one understates the output
/// enough that a source could be judged worth re-encoding and come out larger.
///
/// It also has to speak each family's dialect: NVENC takes its quality on -cq
/// and rejects the x265 preset and tune names outright. Getting that wrong does
/// not misprice the encode, it fails the probe, and a failed probe silently
/// falls back to copying.
/// </summary>
[Trait("Category", "Unit")]
public class EncodeYieldProbeArgumentTests
{
    private readonly List<string[]> _invocations = [];

    private EncodeYieldProbe BuildProbe()
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
                (_, args, _, _) => _invocations.Add(args)
            )
            // Non-zero exit: the probe returns null and never reads a sample file,
            // which keeps these tests about argument construction alone.
            .ReturnsAsync(new ProcessResult(1, "", "", TimeSpan.Zero));

        Mock<IStorage> storage = new();
        storage
            .Setup(s => s.AcquireLocalPath(It.IsAny<string>()))
            .Returns((string p) => new LocalPathLease(p, null));

        return new(
            new EncoderOptions { FfmpegPathOverride = "ffmpeg", FfprobePathOverride = "ffprobe" },
            runner.Object,
            storage.Object,
            NullLogger<EncodeYieldProbe>.Instance
        );
    }

    private async Task<string[]> ArgsFor(EncodeYieldTarget target)
    {
        await BuildProbe()
            .EstimateBitrateKbpsAsync("/anime/ep.mkv", target, TimeSpan.FromMinutes(24), default);

        return Assert.Single(_invocations);
    }

    [Fact]
    public async Task ResolvedHardwareEncoder_IsTheOneMeasured()
    {
        string[] args = await ArgsFor(
            new(VideoCodecType.H265, 20, "slow", "animation", "yuv420p10le", "hevc_nvenc")
        );

        args.Should().ContainInOrder("-c:v", "hevc_nvenc");
    }

    [Fact]
    public async Task HardwareEncoder_TakesQualityOnCq_NotCrf()
    {
        string[] args = await ArgsFor(
            new(VideoCodecType.H265, 20, "slow", "animation", "yuv420p10le", "hevc_nvenc")
        );

        args.Should().ContainInOrder("-cq", "20");
        args.Should().NotContain("-crf");
    }

    [Fact]
    public async Task HardwareEncoder_DropsTheSoftwarePresetAndTune()
    {
        string[] args = await ArgsFor(
            new(VideoCodecType.H265, 20, "slow", "animation", "yuv420p10le", "hevc_nvenc")
        );

        args.Should().NotContain("slow", "NVENC presets are p1..p7 and reject x265 names");
        args.Should().NotContain("animation", "tune is an x265 concept");
    }

    [Fact]
    public async Task SoftwareEncoder_KeepsCrfPresetAndTune()
    {
        string[] args = await ArgsFor(
            new(VideoCodecType.H265, 20, "slow", "animation", "yuv420p10le", "libx265")
        );

        args.Should().ContainInOrder("-c:v", "libx265");
        args.Should().ContainInOrder("-crf", "20");
        args.Should().ContainInOrder("-preset", "slow");
        args.Should().ContainInOrder("-tune", "animation");
    }

    [Fact]
    public async Task NoResolvedEncoder_FallsBackToTheSoftwareOneForTheCodec()
    {
        string[] args = await ArgsFor(new(VideoCodecType.H265, 20, "slow", null, null));

        args.Should().ContainInOrder("-c:v", "libx265");
    }

    [Fact]
    public async Task SampleIsTakenPastTheOpening_NotFromTheStart()
    {
        string[] args = await ArgsFor(new(VideoCodecType.H265, 20, null, null, null, "libx265"));

        // 24 minutes in, a quarter of the way through: 360s.
        args.Should().ContainInOrder("-ss", "360");
        args.Should().ContainInOrder("-t", "30");
    }

    [Fact]
    public async Task SourceTooShortToSample_IsNotProbedAtAll()
    {
        long? result = await BuildProbe()
            .EstimateBitrateKbpsAsync(
                "/anime/clip.mkv",
                new(VideoCodecType.H265, 20, null, null, null, "libx265"),
                TimeSpan.FromSeconds(30),
                default
            );

        result.Should().BeNull();
        _invocations.Should().BeEmpty("a 30s file has no representative 30s window to measure");
    }
}
