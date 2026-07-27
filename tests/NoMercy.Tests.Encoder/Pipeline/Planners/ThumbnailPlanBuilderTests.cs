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
using NoMercy.Encoder.PostProcess;
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
            FilePath: "/movies/test.mkv",
            Format: "matroska",
            Duration: TimeSpan.FromHours(2),
            OverallBitRateKbps: 8000,
            FileSizeBytes: 7_200_000_000,
            VideoStreams:
            [
                new(
                    Index: 0,
                    Codec: "h264",
                    Width: width,
                    Height: height,
                    FrameRate: 24.0,
                    BitDepth: 8,
                    PixelFormat: "yuv420p",
                    ColorPrimaries: null,
                    ColorTransfer: null,
                    ColorSpace: null,
                    IsDefault: true,
                    BitRateKbps: 6000
                ),
            ],
            AudioStreams: [],
            SubtitleStreams: [],
            Chapters: []
        );

    private static MediaInfo BuildAudioOnlyMedia() =>
        new(
            FilePath: "/music/test.flac",
            Format: "flac",
            Duration: TimeSpan.FromMinutes(4),
            OverallBitRateKbps: 900,
            FileSizeBytes: 27_000_000,
            VideoStreams: [],
            AudioStreams: [],
            SubtitleStreams: [],
            Chapters: []
        );

    private static VideoOutput BuildVideoOutput(StreamPolicy policy) =>
        new(
            Policy: policy,
            Codec: policy == StreamPolicy.Copy ? VideoCodecType.Copy : VideoCodecType.H264,
            Width: 1920,
            Height: 1080,
            RateControl: NoMercy.Encoder.Profiles.RateControlMode.Crf,
            Crf: 23,
            BitrateKbps: 4000,
            MaxBitrateKbps: null,
            BufferSizeKbps: null,
            Preset: "medium",
            CodecProfile: CodecProfile.Auto,
            Level: null,
            Tune: null,
            BitDepth: 8,
            PixelFormat: null,
            KeyframeIntervalSeconds: 2,
            ConvertHdrToSdr: false,
            SegmentNameTemplate: "video/{label}",
            PlaylistNameTemplate: "video/{label}/playlist"
        );

    private static EncodingProfile BuildProfile(VideoOutput video, bool generateSpriteVtt = true) =>
        new(
            Id: Ulid.NewUlid(),
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
        plan!.Width.Should().Be(SpriteSheet.MinimumWidth);
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
            generateSpriteVtt: false
        );

        ThumbnailOutputPlan? plan = ThumbnailPlanBuilder.Build(profile, media);

        plan.Should().BeNull();
    }

    /// <summary>
    /// The grid is what keeps the sheet from ending in a green block, so a plan
    /// that omits it is the bug. It has to hold every frame the film produces:
    /// over-estimating costs a few black tiles past the end, under-estimating
    /// cuts real thumbnails off it.
    /// </summary>
    [Fact]
    public void APlan_CarriesAGridBigEnoughForTheWholeFilm()
    {
        MediaInfo media = BuildMediaWithVideo();
        EncodingProfile profile = BuildProfile(BuildVideoOutput(StreamPolicy.Transcode));

        ThumbnailOutputPlan? plan = ThumbnailPlanBuilder.Build(profile, media);

        plan.Should().NotBeNull();

        int mostFramesPossible = (int)(media.Duration.TotalSeconds / plan!.IntervalSeconds) + 1;
        plan.Grid.CellCount.Should().BeGreaterThanOrEqualTo(mostFramesPossible);
        plan.Grid.CellCount.Should()
            .Be(plan.Grid.Columns * plan.Grid.Rows, "a partial last row is what comes out green");
    }

    [Fact]
    public void AShortTitle_StillGetsAUsableGrid()
    {
        MediaInfo media = BuildMediaWithVideo() with { Duration = TimeSpan.FromSeconds(75) };
        EncodingProfile profile = BuildProfile(BuildVideoOutput(StreamPolicy.Transcode));

        ThumbnailOutputPlan? plan = ThumbnailPlanBuilder.Build(profile, media);

        plan!.Grid.Columns.Should().BeGreaterThan(0);
        plan.Grid.Rows.Should().BeGreaterThan(0);
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
