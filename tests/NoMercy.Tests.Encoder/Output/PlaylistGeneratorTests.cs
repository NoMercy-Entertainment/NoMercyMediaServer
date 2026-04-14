namespace NoMercy.Tests.Encoder.Output;

using NoMercy.Encoder.Codecs;
using NoMercy.Encoder.Output;
using NoMercy.Encoder.Pipeline;

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
        playlist.Should().Contain("#EXT-X-VERSION:6");
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
        string playlist = Generate(CreatePlan(encoderName: "libsvtav1", tenBit: true));

        playlist.Should().Contain("av01.0.15M.10");
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
            ["[v0]"] = new HlsVariantAnalyzer.VariantMetrics(5_000_000, 3_500_000),
        };
        Dictionary<string, HlsVariantAnalyzer.VariantMetrics> audMetrics = new()
        {
            ["0:a:0"] = new HlsVariantAnalyzer.VariantMetrics(256_000, 192_000),
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
        return new OutputPlan(
            Format: OutputFormat.Hls,
            VideoOutputs:
            [
                new VideoOutputPlan(
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
                    new Dictionary<string, string>()
                ),
            ],
            AudioOutputs:
            [
                new AudioOutputPlan("aac", 192, 2, 48000, StreamAction.Transcode, "eng", "0:a:0"),
            ],
            SubtitleOutputs: [],
            Thumbnails: null
        );
    }

    private static OutputPlan CreateMultiResPlan()
    {
        return new OutputPlan(
            Format: OutputFormat.Hls,
            VideoOutputs:
            [
                new VideoOutputPlan(
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
                    new Dictionary<string, string>()
                ),
                new VideoOutputPlan(
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
                    new Dictionary<string, string>()
                ),
            ],
            AudioOutputs:
            [
                new AudioOutputPlan("aac", 192, 2, 48000, StreamAction.Transcode, "eng", "0:a:0"),
            ],
            SubtitleOutputs: [],
            Thumbnails: null
        );
    }
}
