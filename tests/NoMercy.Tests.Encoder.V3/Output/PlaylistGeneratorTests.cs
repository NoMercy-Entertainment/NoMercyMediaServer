namespace NoMercy.Tests.Encoder.V3.Output;

using NoMercy.Encoder.V3.Codecs;
using NoMercy.Encoder.V3.Output;
using NoMercy.Encoder.V3.Pipeline;

public class PlaylistGeneratorTests
{
    [Fact]
    public void MasterPlaylist_ContainsExtm3u()
    {
        PlaylistGenerator generator = new();
        string playlist = generator.GenerateMasterPlaylist(CreatePlan());

        playlist.Should().StartWith("#EXTM3U");
        playlist.Should().Contain("#EXT-X-VERSION:7");
    }

    [Fact]
    public void MasterPlaylist_ContainsVideoVariants()
    {
        PlaylistGenerator generator = new();
        string playlist = generator.GenerateMasterPlaylist(CreateMultiResPlan());

        playlist.Should().Contain("RESOLUTION=1920x1080");
        playlist.Should().Contain("RESOLUTION=1280x720");
        playlist.Should().Contain("video_1920x1080/video_1920x1080.m3u8");
        playlist.Should().Contain("video_1280x720/video_1280x720.m3u8");
    }

    [Fact]
    public void MasterPlaylist_H264_CorrectCodecTag()
    {
        PlaylistGenerator generator = new();
        OutputPlan plan = CreatePlan();
        string playlist = generator.GenerateMasterPlaylist(plan);

        playlist.Should().Contain("avc1.640028");
    }

    [Fact]
    public void MasterPlaylist_Hevc_CorrectCodecTag()
    {
        PlaylistGenerator generator = new();
        OutputPlan plan = CreatePlan(encoderName: "hevc_nvenc");
        string playlist = generator.GenerateMasterPlaylist(plan);

        playlist.Should().Contain("hvc1.");
    }

    [Fact]
    public void MasterPlaylist_Av1_10bit_CorrectCodecTag()
    {
        PlaylistGenerator generator = new();
        OutputPlan plan = CreatePlan(encoderName: "libsvtav1", tenBit: true);
        string playlist = generator.GenerateMasterPlaylist(plan);

        playlist.Should().Contain("av01.0.15M.10");
    }

    [Fact]
    public void MasterPlaylist_AudioGroup_Present()
    {
        PlaylistGenerator generator = new();
        string playlist = generator.GenerateMasterPlaylist(CreatePlan());

        playlist.Should().Contain("#EXT-X-MEDIA:TYPE=AUDIO");
        playlist.Should().Contain("GROUP-ID=\"audio\"");
        playlist.Should().Contain("LANGUAGE=\"eng\"");
    }

    [Fact]
    public void MasterPlaylist_AacAudio_Mp4aCodecTag()
    {
        PlaylistGenerator generator = new();
        string playlist = generator.GenerateMasterPlaylist(CreatePlan());

        playlist.Should().Contain("mp4a.40.2");
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
