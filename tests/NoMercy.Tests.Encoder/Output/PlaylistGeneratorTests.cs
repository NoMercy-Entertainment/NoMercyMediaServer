using NoMercy.Encoder.Codecs;
using NoMercy.Encoder.Output;
using NoMercy.Encoder.Pipeline;

namespace NoMercy.Tests.Encoder.Output;

public class PlaylistGeneratorTests
{
    private const string MediaTitle = "Movie.Name.NoMercy";

    private static readonly Dictionary<
        string,
        HlsVariantAnalyzer.VariantMetrics
    > EmptyVideoMetrics = [];

    private static readonly Dictionary<
        string,
        HlsVariantAnalyzer.VariantMetrics
    > EmptyAudioMetrics = [];

    private string Generate(OutputPlan plan)
    {
        PlaylistGenerator generator = new();
        return generator.GenerateMasterPlaylist(
            plan,
            MediaTitle,
            EmptyVideoMetrics,
            EmptyAudioMetrics
        );
    }

    [Fact]
    public void MasterPlaylist_ContainsExtm3u()
    {
        string playlist = Generate(CreatePlan());

        playlist.Should().StartWith("#EXTM3U");
        // Version is computed from active features. Basic mpegts with no subtitles
        // group, no fMP4, and no chapter date-ranges requires version 3.
        playlist.Should().Contain("#EXT-X-VERSION:3");
    }

    [Fact]
    public void MasterPlaylist_ContainsIndependentSegments()
    {
        string playlist = Generate(CreatePlan());

        playlist.Should().Contain("#EXT-X-INDEPENDENT-SEGMENTS");
    }

    [Fact]
    public void MasterPlaylist_ContainsVideoVariants()
    {
        string playlist = Generate(CreateMultiResPlan());

        playlist.Should().Contain("RESOLUTION=1920x1080");
        playlist.Should().Contain("RESOLUTION=1280x720");
        playlist.Should().Contain("video_1920x1080_SDR/video_1920x1080_SDR.m3u8");
        playlist.Should().Contain("video_1280x720_SDR/video_1280x720_SDR.m3u8");
    }

    [Fact]
    public void MasterPlaylist_H264_CorrectCodecTag()
    {
        string playlist = Generate(CreatePlan());

        playlist.Should().Contain("avc1.640028");
    }

    [Fact]
    public void MasterPlaylist_Hevc_CorrectCodecTag()
    {
        string playlist = Generate(CreatePlan(encoderName: "hevc_nvenc"));

        playlist.Should().Contain("hvc1.");
    }

    [Fact]
    public void MasterPlaylist_Av1_10bit_CorrectCodecTag()
    {
        // Plan fixture declares Level="4.0" (Av1 spec table A.1 → index 8)
        // and tenBit=true → expect av01.0.08M.10. Phase 4.17 introduced the
        // spec-accurate HlsCodecsStringBuilder which derives the level index
        // from the plan instead of hard-coding 5.3 (index 15) like the legacy
        // generator did.
        string playlist = Generate(CreatePlan(encoderName: "libsvtav1", tenBit: true));

        playlist.Should().Contain("av01.0.08M.10");
    }

    [Fact]
    public void MasterPlaylist_AudioGroup_Present()
    {
        string playlist = Generate(CreatePlan());

        playlist.Should().Contain("#EXT-X-MEDIA:TYPE=AUDIO");
        playlist.Should().Contain("GROUP-ID=\"audio_aac\"");
        playlist.Should().Contain("LANGUAGE=\"eng\"");
    }

    [Fact]
    public void MasterPlaylist_AacAudio_Mp4aCodecTag()
    {
        string playlist = Generate(CreatePlan());

        playlist.Should().Contain("mp4a.40.2");
    }

    [Fact]
    public void MasterPlaylist_MeasuredBandwidth_UsedWhenProvided()
    {
        PlaylistGenerator generator = new();
        OutputPlan plan = CreatePlan();
        Dictionary<string, HlsVariantAnalyzer.VariantMetrics> vidMetrics = new()
        {
            ["[v0]"] = new(5_000_000, 3_500_000),
        };
        Dictionary<string, HlsVariantAnalyzer.VariantMetrics> audMetrics = new()
        {
            ["0:a:0"] = new(256_000, 192_000),
        };

        string playlist = generator.GenerateMasterPlaylist(
            plan,
            MediaTitle,
            vidMetrics,
            audMetrics
        );

        playlist.Should().Contain("BANDWIDTH=5256000");
        playlist.Should().Contain("AVERAGE-BANDWIDTH=3692000");
    }

    private static OutputPlan CreatePlan(string encoderName = "libx264", bool tenBit = false)
    {
        return new(
            Format: OutputFormat.Hls,
            VideoOutputs:
            [
                new(
                    1920,
                    1080,
                    encoderName,
                    23,
                    8000,
                    "medium",
                    "high",
                    "4.0",
                    tenBit,
                    tenBit ? "yuv420p10le" : "yuv420p",
                    "[v0]",
                    new()
                ),
            ],
            AudioOutputs: [new("aac", 192, 2, 48000, StreamAction.Transcode, "eng", "0:a:0")],
            SubtitleOutputs: [],
            Thumbnails: null
        );
    }

    private static OutputPlan CreateMultiResPlan()
    {
        return new(
            Format: OutputFormat.Hls,
            VideoOutputs:
            [
                new(
                    1920,
                    1080,
                    "libx264",
                    23,
                    8000,
                    "medium",
                    "high",
                    "4.0",
                    false,
                    "yuv420p",
                    "[v0]",
                    new()
                ),
                new(
                    1280,
                    720,
                    "libx264",
                    23,
                    4000,
                    "medium",
                    "high",
                    "3.1",
                    false,
                    "yuv420p",
                    "[v1]",
                    new()
                ),
            ],
            AudioOutputs: [new("aac", 192, 2, 48000, StreamAction.Transcode, "eng", "0:a:0")],
            SubtitleOutputs: [],
            Thumbnails: null
        );
    }
}
