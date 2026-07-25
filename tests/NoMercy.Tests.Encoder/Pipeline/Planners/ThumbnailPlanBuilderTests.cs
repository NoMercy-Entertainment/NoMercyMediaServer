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

using NoMercy.Encoder.Analysis;
using NoMercy.Encoder.Codecs;
using NoMercy.Encoder.Output;
using NoMercy.Encoder.Pipeline.Stages;
using NoMercy.Encoder.Profiles;

namespace NoMercy.Tests.Encoder.Pipeline.Planners;

/// <summary>
/// A stream-copied video output no longer suppresses the sprite plan — BuildStage
/// now routes a copy plan's sprite through a separate ffmpeg command (see
/// BuildStageThumbnailCopyTests), so ThumbnailPlanBuilder must build a plan for
/// copy and transcode profiles alike, keying only on whether the source has
/// frames to sample.
/// </summary>
public class ThumbnailPlanBuilderTests
{
    private static MediaInfo BuildMediaWithVideo(int width = 1920, int height = 1080) =>
        new(
            "/movies/test.mkv",
            "matroska",
            TimeSpan.FromHours(2),
            8000,
            7_200_000_000,
            [
                new(
                    0,
                    "h264",
                    width,
                    height,
                    24.0,
                    8,
                    "yuv420p",
                    null,
                    null,
                    null,
                    true,
                    6000
                ),
            ],
            [],
            [],
            []
        );

    private static MediaInfo BuildAudioOnlyMedia() =>
        new(
            "/music/test.flac",
            "flac",
            TimeSpan.FromMinutes(4),
            900,
            27_000_000,
            [],
            [],
            [],
            []
        );

    private static VideoOutput BuildVideoOutput(StreamPolicy policy) =>
        new(
            policy,
            policy == StreamPolicy.Copy ? VideoCodecType.Copy : VideoCodecType.H264,
            1920,
            1080,
            NoMercy.Encoder.Profiles.RateControlMode.Crf,
            23,
            4000,
            null,
            null,
            "medium",
            CodecProfile.Auto,
            null,
            null,
            8,
            null,
            2,
            false,
            "video/{label}",
            "video/{label}/playlist"
        );

    private static EncodingProfile BuildProfile(VideoOutput video, bool generateSpriteVtt = true) =>
        new(
            Ulid.NewUlid(),
            Name: "ThumbnailPlanBuilderFixture",
            Container: Container.HlsFmp4,
            Video: video,
            Audio: [],
            Subtitles: [],
            HlsDerivatives: new HlsDerivatives { GenerateSpriteVtt = generateSpriteVtt }
        );

    [Fact]
    public void CopyProfile_WithVideoStream_BuildsPlan()
    {
        MediaInfo media = BuildMediaWithVideo();
        EncodingProfile profile = BuildProfile(BuildVideoOutput(StreamPolicy.Copy));

        ThumbnailOutputPlan? plan = ThumbnailPlanBuilder.Build(profile, media);

        plan.Should().NotBeNull("a remux/copy profile can still sprite via a separate command");
        plan!.Width.Should().Be(160);
        plan.IntervalSeconds.Should().Be(10);
    }

    [Fact]
    public void TranscodeProfile_WithVideoStream_BuildsPlan()
    {
        MediaInfo media = BuildMediaWithVideo();
        EncodingProfile profile = BuildProfile(BuildVideoOutput(StreamPolicy.Transcode));

        ThumbnailOutputPlan? plan = ThumbnailPlanBuilder.Build(profile, media);

        plan.Should().NotBeNull();
    }

    [Fact]
    public void CopyProfile_GenerateSpriteVttDisabled_ReturnsNull()
    {
        MediaInfo media = BuildMediaWithVideo();
        EncodingProfile profile = BuildProfile(
            BuildVideoOutput(StreamPolicy.Copy),
            false
        );

        ThumbnailOutputPlan? plan = ThumbnailPlanBuilder.Build(profile, media);

        plan.Should().BeNull();
    }

    [Fact]
    public void AudioOnlyMedia_ReturnsNull()
    {
        MediaInfo media = BuildAudioOnlyMedia();
        EncodingProfile profile = BuildProfile(BuildVideoOutput(StreamPolicy.Copy));

        ThumbnailOutputPlan? plan = ThumbnailPlanBuilder.Build(profile, media);

        plan.Should().BeNull("audio-only media has no frames to sprite from");
    }

    [Fact]
    public void NoVideoOutputOnProfile_ReturnsNull()
    {
        MediaInfo media = BuildMediaWithVideo();
        EncodingProfile profile = BuildProfile(BuildVideoOutput(StreamPolicy.Copy)) with
        {
            Video = null,
        };

        ThumbnailOutputPlan? plan = ThumbnailPlanBuilder.Build(profile, media);

        plan.Should().BeNull("a profile with no video output has nothing to sprite");
    }
}
