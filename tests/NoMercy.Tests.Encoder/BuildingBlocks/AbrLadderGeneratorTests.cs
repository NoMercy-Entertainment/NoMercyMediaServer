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
using NoMercy.Encoder.Analysis;
using NoMercy.Encoder.BuildingBlocks;
using NoMercy.Encoder.Codecs;
using NoMercy.Encoder.Profiles;
using RateControlMode = NoMercy.Encoder.Profiles.RateControlMode;

namespace NoMercy.Tests.Encoder.BuildingBlocks;

/// <summary>
/// Tests for the legacy Generate(MediaInfo, VideoOutput) path — these verify
/// the existing AbrLadderGenerator behaviour still works after the rewrite.
/// </summary>
public class AbrLadderGeneratorLegacyTests
{
    private readonly AbrLadderGenerator _generator = new(logger: NullLogger<AbrLadderGenerator>.Instance);

    [Fact]
    public void Generate_1080pSource_ProducesTiersUpToSource()
    {
        MediaInfo media = BuildMedia(width: 1920, height: 1080, bitRateKbps: 6000);
        VideoOutput reference = BuildReference();

        VideoOutput[] ladder = _generator.Generate(media: media, reference: reference);

        Assert.Equal(expected: 4, actual: ladder.Length);
        Assert.Equal(expected: 360, actual: ladder[0].Height);
        Assert.Equal(expected: 480, actual: ladder[1].Height);
        Assert.Equal(expected: 720, actual: ladder[2].Height);
        Assert.Equal(expected: 1080, actual: ladder[3].Height);
    }

    [Fact]
    public void Generate_4KSource_IncludesAllTiersIncluding4K()
    {
        MediaInfo media = BuildMedia(width: 3840, height: 2160, bitRateKbps: 50000);
        VideoOutput reference = BuildReference();

        VideoOutput[] ladder = _generator.Generate(media: media, reference: reference);

        int[] heights = ladder.Select(selector: v => v.Height ?? 0).ToArray();
        Assert.Contains(expected: 360, collection: heights);
        Assert.Contains(expected: 1080, collection: heights);
        Assert.Contains(expected: 2160, collection: heights);
    }

    [Fact]
    public void Generate_SkipsTiersAboveSourceResolution()
    {
        MediaInfo media = BuildMedia(width: 1280, height: 720, bitRateKbps: 3000);
        VideoOutput reference = BuildReference();

        VideoOutput[] ladder = _generator.Generate(media: media, reference: reference);

        Assert.All(collection: ladder, action: v => Assert.True(condition: v.Height <= 720));
        Assert.DoesNotContain(expected: 1080, collection: ladder.Select(selector: v => v.Height ?? 0));
    }

    [Fact]
    public void Generate_AnimeSource_ScalesBitratesDown()
    {
        MediaInfo lowBitrate = BuildMedia(width: 1920, height: 1080, bitRateKbps: 1000);
        MediaInfo highBitrate = BuildMedia(width: 1920, height: 1080, bitRateKbps: 8000);
        VideoOutput reference = BuildReference();

        VideoOutput[] low = _generator.Generate(media: lowBitrate, reference: reference);
        VideoOutput[] high = _generator.Generate(media: highBitrate, reference: reference);

        VideoOutput low1080 = Assert.Single(collection: low, predicate: v => v.Height == 1080);
        VideoOutput high1080 = Assert.Single(collection: high, predicate: v => v.Height == 1080);

        Assert.True(
            condition: low1080.BitrateKbps < high1080.BitrateKbps,
            userMessage: "low-bitrate source should produce a lower-bitrate 1080p tier"
        );
    }

    [Fact]
    public void Generate_CopiesCodecFromReference()
    {
        MediaInfo media = BuildMedia(width: 1920, height: 1080, bitRateKbps: 6000);
        VideoOutput reference = BuildReference() with { Codec = VideoCodecType.H265 };

        VideoOutput[] ladder = _generator.Generate(media: media, reference: reference);

        Assert.All(collection: ladder, action: v => Assert.Equal(expected: VideoCodecType.H265, actual: v.Codec));
    }

    [Fact]
    public void Generate_WidthsAreEven()
    {
        MediaInfo media = BuildMedia(width: 1920, height: 1080, bitRateKbps: 6000);
        VideoOutput reference = BuildReference();

        VideoOutput[] ladder = _generator.Generate(media: media, reference: reference);

        Assert.All(collection: ladder, action: v => Assert.Equal(expected: 0, actual: v.Width % 2));
    }

    [Fact]
    public void Generate_NoVideoStreams_ReturnsEmpty()
    {
        MediaInfo media = new(
            FilePath: "/audio-only.m4a",
            Format: "mp4",
            Duration: TimeSpan.FromMinutes(minutes: 3),
            OverallBitRateKbps: 256,
            FileSizeBytes: 1_000_000,
            VideoStreams: [],
            AudioStreams: [],
            SubtitleStreams: [],
            Chapters: []
        );

        VideoOutput[] ladder = _generator.Generate(media: media, reference: BuildReference());

        Assert.Empty(collection: ladder);
    }

    [Fact]
    public void Generate_OddSourceHeight_AddsNativeResolutionTier()
    {
        MediaInfo media = BuildMedia(width: 1920, height: 1200, bitRateKbps: 8000);
        VideoOutput reference = BuildReference();

        VideoOutput[] ladder = _generator.Generate(media: media, reference: reference);

        Assert.Equal(expected: 1200, actual: ladder[^1].Height);
        Assert.Equal(expected: 1920, actual: ladder[^1].Width);
    }

    private static MediaInfo BuildMedia(int width, int height, long bitRateKbps) =>
        new(
            FilePath: "/video.mkv",
            Format: "matroska",
            Duration: TimeSpan.FromMinutes(minutes: 90),
            OverallBitRateKbps: bitRateKbps + 500,
            FileSizeBytes: 4_000_000_000,
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
                    BitRateKbps: bitRateKbps
                ),
            ],
            AudioStreams: [],
            SubtitleStreams: [],
            Chapters: []
        );

    private static VideoOutput BuildReference() =>
        new(
            Policy: StreamPolicy.Transcode,
            Codec: VideoCodecType.H264,
            Width: 1920,
            Height: 1080,
            RateControl: RateControlMode.Crf,
            Crf: 23,
            BitrateKbps: 4000,
            MaxBitrateKbps: null,
            BufferSizeKbps: null,
            Preset: "medium",
            CodecProfile: CodecProfile.High,
            Level: "4.1",
            Tune: null,
            BitDepth: 8,
            PixelFormat: null,
            KeyframeIntervalSeconds: 2,
            ConvertHdrToSdr: false,
            SegmentNameTemplate: ":type:_:framesize:_:colorrange:/:type:_:framesize:_:colorrange:",
            PlaylistNameTemplate: ":type:_:framesize:_:colorrange:/:type:_:framesize:_:colorrange:"
        );
}

/// <summary>
/// Tests for the new GenerateLadder(MediaInfo, VideoCodecType, AutoLadderConfig) path.
/// These are the 11 plan test cases plus supporting builders.
/// </summary>
public class AbrLadderGeneratorAutoConfigTests
{
    private readonly AbrLadderGenerator _generator = new(logger: NullLogger<AbrLadderGenerator>.Instance);

    // ── Test 1 ───────────────────────────────────────────────────────────────
    [Fact]
    public void Test1_AppleHls_4KSource_Produces5RungLadder()
    {
        // Apple spec maxRungs default=5; 4K source has 6 candidate tiers but cap=5
        // drops the lowest (360p).
        MediaInfo media = Build4KMedia();
        AutoLadderConfig config = new()
        {
            Tiers = LadderTiers.AppleHlsRecommended,
            BitrateStrategy = BitrateStrategy.AppleHlsRecommended,
            MaxRungs = 5,
            NeverUpscale = true,
            NeverUpsource = false,
        };

        LadderRung[] rungs = _generator.GenerateLadder(media: media, profileCodec: VideoCodecType.H264, autoConfig: config);

        rungs.Should().HaveCount(expected: 5);
    }

    // ── Test 2 ───────────────────────────────────────────────────────────────
    [Fact]
    public void Test2_NeverUpscale_720pSource_OnlyUpToSource()
    {
        MediaInfo media = Build720pMedia();
        AutoLadderConfig config = new()
        {
            Tiers = LadderTiers.AppleHlsRecommended,
            BitrateStrategy = BitrateStrategy.AppleHlsRecommended,
            NeverUpscale = true,
            NeverUpsource = false,
            MaxRungs = 10,
        };

        LadderRung[] rungs = _generator.GenerateLadder(media: media, profileCodec: VideoCodecType.H264, autoConfig: config);

        rungs.Should().AllSatisfy(expected: r => r.Height.Should().BeLessThanOrEqualTo(expected: 720));
        rungs.Should().NotContain(predicate: r => r.Height == 1080);
    }

    // ── Test 3 ───────────────────────────────────────────────────────────────
    [Fact]
    public void Test3_NeverUpsource_1MbpsSource_DropsHighBitrateTiers()
    {
        // Source at 1000 kbps — tiers whose recommended bitrate > 1000 kbps are dropped.
        MediaInfo media = Build1080pMedia(bitRateKbps: 1000);
        AutoLadderConfig config = new()
        {
            Tiers = LadderTiers.AppleHlsRecommended,
            BitrateStrategy = BitrateStrategy.AppleHlsRecommended,
            NeverUpscale = false,
            NeverUpsource = true,
            MaxRungs = 10,
            MinTierGapPercent = 0,
        };

        LadderRung[] rungs = _generator.GenerateLadder(media: media, profileCodec: VideoCodecType.H264, autoConfig: config);

        rungs.Should().AllSatisfy(expected: r => r.BitrateKbps.Should().BeLessThanOrEqualTo(expected: 1000));
    }

    // ── Test 4 ───────────────────────────────────────────────────────────────
    [Fact]
    public void Test4_PercentOfSource_1080p6Mbps_CorrectBitrates()
    {
        // PercentOfSource = 50, source = 6000 kbps, source height = 1080
        // 1080p: 6000 × (1080/1080)² × 50/100 = 3000 kbps
        // 540p:  6000 × (540/1080)² × 50/100 = 6000 × 0.25 × 0.5 = 750 kbps
        MediaInfo media = Build1080pMedia(bitRateKbps: 6000);
        AutoLadderConfig config = new()
        {
            Tiers = LadderTiers.AppleHlsRecommended,
            BitrateStrategy = BitrateStrategy.PercentOfSource,
            SourcePercentage = 50.0,
            NeverUpscale = true,
            NeverUpsource = false,
            MaxRungs = 10,
            MinTierGapPercent = 0,
        };

        LadderRung[] rungs = _generator.GenerateLadder(media: media, profileCodec: VideoCodecType.H264, autoConfig: config);

        LadderRung rung1080 = rungs.Single(predicate: r => r.Height == 1080);
        rung1080.BitrateKbps.Should().Be(expected: 3000);

        LadderRung rung540 = rungs.Single(predicate: r => r.Height == 540);
        rung540.BitrateKbps.Should().Be(expected: 750);
    }

    // ── Test 5 ───────────────────────────────────────────────────────────────
    [Fact]
    public void Test5_MixedCodecPolicy_CorrectCodecPerTier()
    {
        // Tiers: 360p/540p/720p → H264, 1080p/1440p/2160p → H265; split=720
        MediaInfo media = Build4KMedia();
        AutoLadderConfig config = new()
        {
            Tiers = LadderTiers.AppleHlsRecommended,
            BitrateStrategy = BitrateStrategy.AppleHlsRecommended,
            CodecPolicy = LadderCodecPolicy.Mixed,
            LowTierCodec = VideoCodecType.H264,
            HighTierCodec = VideoCodecType.H265,
            MixedPolicySplitHeight = 720,
            NeverUpscale = true,
            NeverUpsource = false,
            MaxRungs = 10,
            MinTierGapPercent = 0,
        };

        LadderRung[] rungs = _generator.GenerateLadder(media: media, profileCodec: VideoCodecType.H264, autoConfig: config);

        rungs
            .Where(predicate: r => r.Height <= 720)
            .Should()
            .AllSatisfy(expected: r => r.Codec.Should().Be(expected: VideoCodecType.H264));
        rungs
            .Where(predicate: r => r.Height > 720)
            .Should()
            .AllSatisfy(expected: r => r.Codec.Should().Be(expected: VideoCodecType.H265));
    }

    // ── Test 6 ───────────────────────────────────────────────────────────────
    [Fact]
    public void Test6_MixedCodecPolicy_NullLowTier_ThrowsOrAsserts()
    {
        // Validator catches this upstream; generator throws defensively if reached.
        MediaInfo media = Build4KMedia();
        AutoLadderConfig config = new()
        {
            Tiers = LadderTiers.AppleHlsRecommended,
            BitrateStrategy = BitrateStrategy.AppleHlsRecommended,
            CodecPolicy = LadderCodecPolicy.Mixed,
            LowTierCodec = null,
            HighTierCodec = VideoCodecType.H265,
            NeverUpscale = false,
            NeverUpsource = false,
        };

        Action act = () => _generator.GenerateLadder(media: media, profileCodec: VideoCodecType.H264, autoConfig: config);

        act.Should().Throw<InvalidOperationException>();
    }

    // ── Test 7 ───────────────────────────────────────────────────────────────
    [Fact]
    public void Test7_ReduceFramerate_480pAndBelow_At30fps()
    {
        // 60fps source; tiers ≤ 480p → 30fps; tiers > 480p → 60fps
        MediaInfo media = Build4KMedia(frameRate: 60.0);
        AutoLadderConfig config = new()
        {
            Tiers = LadderTiers.AppleHlsRecommended,
            BitrateStrategy = BitrateStrategy.AppleHlsRecommended,
            ReduceFramerateForLowTiers = true,
            LowTierFramerateMultiplier = 0.5,
            LowTierFramerateThresholdHeight = 480,
            NeverUpscale = true,
            NeverUpsource = false,
            MaxRungs = 10,
            MinTierGapPercent = 0,
        };

        LadderRung[] rungs = _generator.GenerateLadder(media: media, profileCodec: VideoCodecType.H264, autoConfig: config);

        rungs.Where(predicate: r => r.Height <= 480).Should().AllSatisfy(expected: r => r.Framerate.Should().Be(expected: 30.0));
        rungs.Where(predicate: r => r.Height > 480).Should().AllSatisfy(expected: r => r.Framerate.Should().Be(expected: 60.0));
    }

    // ── Test 8 ───────────────────────────────────────────────────────────────
    [Fact]
    public void Test8_MaxRungs3_6Candidates_Top3ByHeightRetained()
    {
        // AppleHls 6 tiers, 4K source → all 6 pass NeverUpscale → cap to 3 highest.
        MediaInfo media = Build4KMedia();
        AutoLadderConfig config = new()
        {
            Tiers = LadderTiers.AppleHlsRecommended,
            BitrateStrategy = BitrateStrategy.AppleHlsRecommended,
            MaxRungs = 3,
            NeverUpscale = true,
            NeverUpsource = false,
            MinTierGapPercent = 0,
        };

        LadderRung[] rungs = _generator.GenerateLadder(media: media, profileCodec: VideoCodecType.H264, autoConfig: config);

        rungs.Should().HaveCount(expected: 3);
        rungs.Select(selector: r => r.Height).Should().BeEquivalentTo(expectation: [2160, 1440, 1080]);
    }

    // ── Test 9 ───────────────────────────────────────────────────────────────
    [Fact]
    public void Test9_MinTierGapPercent_CloseRungs_Collapsed()
    {
        // Two tiers very close in bitrate (within 50%) → one is collapsed.
        // Use a custom tier set where two rungs are within 50% of each other.
        LadderTier[] closeTiers =
        [
            new(Width: 1920, Height: 1080, Label: "1080p-a", RecommendedBitrateH264Kbps: 5500, RecommendedBitrateHevcKbps: null, RecommendedBitrateAv1Kbps: null),
            new(Width: 1920, Height: 1080, Label: "1080p-b", RecommendedBitrateH264Kbps: 6000, RecommendedBitrateHevcKbps: null, RecommendedBitrateAv1Kbps: null),
        ];

        MediaInfo media = Build1080pMedia(bitRateKbps: 20000);
        AutoLadderConfig config = new()
        {
            Tiers = closeTiers,
            BitrateStrategy = BitrateStrategy.AppleHlsRecommended,
            MinTierGapPercent = 50.0,
            NeverUpscale = false,
            NeverUpsource = false,
            MaxRungs = 10,
        };

        LadderRung[] rungs = _generator.GenerateLadder(media: media, profileCodec: VideoCodecType.H264, autoConfig: config);

        // The two 1080p tiers are within 50% → collapsed to 1 rung.
        rungs.Should().HaveCount(expected: 1);
    }

    // ── Test 10 ──────────────────────────────────────────────────────────────
    [Fact]
    public void Test10_MinRungs_Warning_NoException_WhenBelowFloor()
    {
        // Only 1 tier passes filters but MinRungs=3 → warn, emit as-is, no exception.
        MediaInfo media = Build360pMedia(); // 360p source → only 360p tier passes NeverUpscale
        AutoLadderConfig config = new()
        {
            Tiers = LadderTiers.AppleHlsRecommended,
            BitrateStrategy = BitrateStrategy.AppleHlsRecommended,
            NeverUpscale = true,
            NeverUpsource = false,
            MaxRungs = 10,
            MinRungs = 3,
            MinTierGapPercent = 0,
        };

        LadderRung[] rungs = _generator.GenerateLadder(media: media, profileCodec: VideoCodecType.H264, autoConfig: config);

        // Should not throw; should emit whatever passed (1 rung).
        rungs.Should().HaveCount(expected: 1);
    }

    // ── Test 11 ──────────────────────────────────────────────────────────────
    [Fact]
    public void Test11_EmptyTiers_ThrowsInvalidOperationException()
    {
        MediaInfo media = Build1080pMedia(bitRateKbps: 6000);
        AutoLadderConfig config = new()
        {
            Tiers = [],
            BitrateStrategy = BitrateStrategy.AppleHlsRecommended,
        };

        Action act = () => _generator.GenerateLadder(media: media, profileCodec: VideoCodecType.H264, autoConfig: config);

        act.Should().Throw<InvalidOperationException>();
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static MediaInfo Build4KMedia(double frameRate = 24.0) =>
        BuildMedia(width: 3840, height: 2160, bitRateKbps: 50000, frameRate: frameRate);

    private static MediaInfo Build720pMedia() => BuildMedia(width: 1280, height: 720, bitRateKbps: 3000);

    private static MediaInfo Build1080pMedia(long bitRateKbps = 6000) =>
        BuildMedia(width: 1920, height: 1080, bitRateKbps: bitRateKbps);

    private static MediaInfo Build360pMedia() => BuildMedia(width: 640, height: 360, bitRateKbps: 500);

    private static MediaInfo BuildMedia(
        int width,
        int height,
        long bitRateKbps,
        double frameRate = 24.0
    ) =>
        new(
            FilePath: "/video.mkv",
            Format: "matroska",
            Duration: TimeSpan.FromMinutes(minutes: 90),
            OverallBitRateKbps: bitRateKbps + 500,
            FileSizeBytes: 4_000_000_000,
            VideoStreams:
            [
                new(
                    Index: 0,
                    Codec: "h264",
                    Width: width,
                    Height: height,
                    FrameRate: frameRate,
                    BitDepth: 8,
                    PixelFormat: "yuv420p",
                    ColorPrimaries: null,
                    ColorTransfer: null,
                    ColorSpace: null,
                    IsDefault: true,
                    BitRateKbps: bitRateKbps
                ),
            ],
            AudioStreams: [],
            SubtitleStreams: [],
            Chapters: []
        );
}
