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
using NoMercy.Encoder.Analysis;
using NoMercy.Encoder.BuildingBlocks;
using NoMercy.Encoder.Codecs;
using NoMercy.Encoder.Hardware;
using NoMercy.Encoder.Hdr;
using NoMercy.Encoder.Output;
using NoMercy.Encoder.Pipeline;
using NoMercy.Encoder.Pipeline.Stages;
using CodecProfile = NoMercy.Encoder.Profiles.CodecProfile;
using Container = NoMercy.Encoder.Profiles.Container;
using EncodingProfile = NoMercy.Encoder.Profiles.EncodingProfile;
using StreamPolicy = NoMercy.Encoder.Profiles.StreamPolicy;
using V2RateControlMode = NoMercy.Encoder.Profiles.RateControlMode;

namespace NoMercy.Tests.Encoder.Pipeline.Stages;

/// <summary>
/// 3D stereo_mode preservation on stream-copy: PlanStage used to splice the
/// flag and its key=value pair together into one dictionary key
/// ("-metadata:s:v stereo_mode"), which FfmpegCommandBuilder emits as a
/// single argv token containing a literal space — ffmpeg never sees the two
/// separate arguments it expects.
/// </summary>
public class PlanStageStereoModeTests
{
    private readonly Mock<ICodecResolver> _codecResolver = new();
    private readonly Mock<IHardwareCapabilities> _hardware = new();
    private readonly Mock<IFfmpegCapabilities> _ffmpegCapabilities = new();
    private readonly PlanStage _stage;

    public PlanStageStereoModeTests()
    {
        _hardware.Setup(h => h.HasGpu).Returns(false);
        _hardware.Setup(h => h.CpuCores).Returns(8);
        _hardware.Setup(h => h.Gpus).Returns([]);
        _hardware.Setup(h => h.SupportsHardwareEncoding(It.IsAny<VideoCodecType>())).Returns(false);
        _hardware
            .Setup(h => h.GetGpuForCodec(It.IsAny<VideoCodecType>()))
            .Returns((GpuDevice?)null);

        _codecResolver
            .Setup(r =>
                r.Resolve(
                    It.IsAny<VideoCodecType>(),
                    It.IsAny<IHardwareCapabilities>(),
                    It.IsAny<EncoderPreference>()
                )
            )
            .Returns(
                new ResolvedCodec(
                    "copy",
                    new(
                        "copy",
                        null,
                        [],
                        [],
                        [],
                        new(0, 51, 23),
                        [RateControlMode.Crf],
                        true,
                        true,
                        int.MaxValue,
                        "yuv420p10le",
                        new()
                    ),
                    null,
                    RateControlMode.Crf
                )
            );

        _stage = new(
            new(),
            new(),
            new(),
            _codecResolver.Object,
            _hardware.Object,
            new TonemapSelector(),
            _ffmpegCapabilities.Object,
            new AbrLadderGenerator(),
            new NoOpCropDetector(),
            NullLogger<PlanStage>.Instance
        );
    }

    [Fact]
    public async Task CopyModeWithStereoSource_EmitsMetadataFlagAsProperKeyValuePair()
    {
        EncodingProfile profile = BuildCopyProfile();
        MediaInfo media = BuildStereoMedia();

        ValidateInput input = new(media, profile);
        EncodingContext context = EncodingContext.Create();
        StageResult result = await _stage.ExecuteAsync(input, context, CancellationToken.None);
        StageSuccess<ExecutionPlan> success = Assert.IsType<StageSuccess<ExecutionPlan>>(result);
        OutputPlan plan = success.Value.OutputPlan;

        VideoOutputPlan video = Assert.Single(plan.VideoOutputs);
        video.ExtraFlags.Should().NotContainKey("-metadata:s:v stereo_mode");
        video
            .ExtraFlags.Should()
            .ContainKey("-metadata:s:v")
            .WhoseValue.Should()
            .Be("stereo_mode=side_by_side_left");
    }

    private static EncodingProfile BuildCopyProfile() =>
        new(
            Ulid.NewUlid(),
            "Stereo Copy Test",
            Container.HlsTs,
            new(
                StreamPolicy.Copy,
                VideoCodecType.H264,
                1920,
                1080,
                V2RateControlMode.Crf,
                23,
                5000,
                null,
                null,
                "medium",
                CodecProfile.High,
                "4.1",
                null,
                8,
                null,
                2,
                false,
                "video/{label}",
                "video/{label}/playlist"
            ),
            [],
            []
        );

    private static MediaInfo BuildStereoMedia() =>
        new(
            "/media/stereo.mkv",
            Format: "matroska",
            Duration: TimeSpan.FromMinutes(90),
            OverallBitRateKbps: 8000,
            FileSizeBytes: 4_000_000_000,
            VideoStreams:
            [
                new(
                    0,
                    "h264",
                    1920,
                    1080,
                    24.0,
                    8,
                    "yuv420p",
                    "bt709",
                    "bt709",
                    "bt709",
                    true,
                    6000
                ),
            ],
            AudioStreams: [],
            SubtitleStreams: [],
            Chapters: [],
            StereoMode: "side_by_side_left"
        );
}
