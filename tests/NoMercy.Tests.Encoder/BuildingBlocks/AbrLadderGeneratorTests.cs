namespace NoMercy.Tests.Encoder.BuildingBlocks;

using NoMercy.Encoder.Analysis;
using NoMercy.Encoder.BuildingBlocks;
using NoMercy.Encoder.Codecs;
using NoMercy.Encoder.Profiles;

public class AbrLadderGeneratorTests
{
    private readonly AbrLadderGenerator _generator = new();

    [Fact]
    public void Generate_1080pSource_ProducesTiersUpToSource()
    {
        MediaInfo media = BuildMedia(width: 1920, height: 1080, bitRateKbps: 6000);
        VideoOutput reference = BuildReference();

        VideoOutput[] ladder = _generator.Generate(media, reference);

        // 360, 480, 720, 1080
        Assert.Equal(4, ladder.Length);
        Assert.Equal(360, ladder[0].Height);
        Assert.Equal(480, ladder[1].Height);
        Assert.Equal(720, ladder[2].Height);
        Assert.Equal(1080, ladder[3].Height);
    }

    [Fact]
    public void Generate_4KSource_IncludesAllTiersIncluding4K()
    {
        MediaInfo media = BuildMedia(width: 3840, height: 2160, bitRateKbps: 50000);
        VideoOutput reference = BuildReference();

        VideoOutput[] ladder = _generator.Generate(media, reference);

        int[] heights = ladder.Select(v => v.Height ?? 0).ToArray();
        Assert.Contains(360, heights);
        Assert.Contains(1080, heights);
        Assert.Contains(2160, heights);
    }

    [Fact]
    public void Generate_SkipsTiersAboveSourceResolution()
    {
        MediaInfo media = BuildMedia(width: 1280, height: 720, bitRateKbps: 3000);
        VideoOutput reference = BuildReference();

        VideoOutput[] ladder = _generator.Generate(media, reference);

        Assert.All(ladder, v => Assert.True(v.Height <= 720));
        Assert.DoesNotContain(1080, ladder.Select(v => v.Height ?? 0));
    }

    [Fact]
    public void Generate_AnimeSource_ScalesBitratesDown()
    {
        // 1080p anime at 2000 kbps — half the bitrate density of a typical 1080p.
        // Generated tier bitrates should be scaled proportionally.
        MediaInfo lowBitrate = BuildMedia(width: 1920, height: 1080, bitRateKbps: 1000);
        MediaInfo highBitrate = BuildMedia(width: 1920, height: 1080, bitRateKbps: 8000);
        VideoOutput reference = BuildReference();

        VideoOutput[] low = _generator.Generate(lowBitrate, reference);
        VideoOutput[] high = _generator.Generate(highBitrate, reference);

        VideoOutput low1080 = Assert.Single(low, v => v.Height == 1080);
        VideoOutput high1080 = Assert.Single(high, v => v.Height == 1080);

        Assert.True(
            low1080.BitrateKbps < high1080.BitrateKbps,
            "low-bitrate source should produce a lower-bitrate 1080p tier"
        );
    }

    [Fact]
    public void Generate_CopiesCodecFromReference()
    {
        MediaInfo media = BuildMedia(width: 1920, height: 1080, bitRateKbps: 6000);
        VideoOutput reference = BuildReference() with { Codec = VideoCodecType.H265 };

        VideoOutput[] ladder = _generator.Generate(media, reference);

        Assert.All(ladder, v => Assert.Equal(VideoCodecType.H265, v.Codec));
    }

    [Fact]
    public void Generate_WidthsAreEven()
    {
        // Even widths are required for yuv420p (the filter graph would reject odd values).
        MediaInfo media = BuildMedia(width: 1920, height: 1080, bitRateKbps: 6000);
        VideoOutput reference = BuildReference();

        VideoOutput[] ladder = _generator.Generate(media, reference);

        Assert.All(ladder, v => Assert.Equal(0, v.Width % 2));
    }

    [Fact]
    public void Generate_NoVideoStreams_ReturnsEmpty()
    {
        MediaInfo media = new(
            FilePath: "/audio-only.m4a",
            Format: "mp4",
            Duration: TimeSpan.FromMinutes(3),
            OverallBitRateKbps: 256,
            FileSizeBytes: 1_000_000,
            VideoStreams: [],
            AudioStreams: [],
            SubtitleStreams: [],
            Chapters: []
        );

        VideoOutput[] ladder = _generator.Generate(media, BuildReference());

        Assert.Empty(ladder);
    }

    [Fact]
    public void Generate_OddSourceHeight_AddsNativeResolutionTier()
    {
        // 1200p source (non-standard) → ladder should include a 1200p tier at the top
        // since no standard tier matches it exactly.
        MediaInfo media = BuildMedia(width: 1920, height: 1200, bitRateKbps: 8000);
        VideoOutput reference = BuildReference();

        VideoOutput[] ladder = _generator.Generate(media, reference);

        Assert.Equal(1200, ladder[^1].Height);
        Assert.Equal(1920, ladder[^1].Width);
    }

    private static MediaInfo BuildMedia(int width, int height, long bitRateKbps) =>
        new(
            FilePath: "/video.mkv",
            Format: "matroska",
            Duration: TimeSpan.FromMinutes(90),
            OverallBitRateKbps: bitRateKbps + 500,
            FileSizeBytes: 4_000_000_000,
            VideoStreams:
            [
                new VideoStreamInfo(
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
            Codec: VideoCodecType.H264,
            Width: 1920,
            Height: 1080,
            BitrateKbps: 4000,
            Crf: 23,
            Preset: "medium",
            Profile: "high",
            Level: "4.1",
            ConvertHdrToSdr: false,
            KeyframeIntervalSeconds: 2,
            TenBit: false
        );
}
