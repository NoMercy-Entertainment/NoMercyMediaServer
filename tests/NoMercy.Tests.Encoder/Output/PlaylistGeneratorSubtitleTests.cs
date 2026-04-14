namespace NoMercy.Tests.Encoder.Output;

using NoMercy.Encoder.Codecs;
using NoMercy.Encoder.Output;
using NoMercy.Encoder.Pipeline;

public class PlaylistGeneratorSubtitleTests
{
    private readonly PlaylistGenerator _gen = new();
    private const string MediaTitle = "Movie.Name.NoMercy";

    // ------------------------------------------------------------------
    // Plan without subtitles does NOT include SUBTITLES tags
    // ------------------------------------------------------------------

    [Fact]
    public void MasterPlaylist_WithoutSubtitles_NoSubtitleTags()
    {
        OutputPlan plan = CreatePlanWithoutSubtitles();
        string playlist = _gen.GenerateMasterPlaylist(plan, MediaTitle);

        playlist.Should().NotContain("TYPE=SUBTITLES");
    }

    // ------------------------------------------------------------------
    // Drop action subtitle is excluded from playlist
    // ------------------------------------------------------------------

    [Fact]
    public void MasterPlaylist_SubtitleWithDropAction_IsExcluded()
    {
        OutputPlan plan = new(
            Format: OutputFormat.Hls,
            VideoOutputs: [BuildVideo()],
            AudioOutputs: [BuildAudio()],
            SubtitleOutputs:
            [
                new SubtitleOutputPlan(
                    OutputCodec: SubtitleCodecType.WebVtt,
                    Action: StreamAction.Drop,
                    Language: "eng",
                    SourceIndex: 0,
                    MapLabel: "0:s:0"
                ),
            ],
            Thumbnails: null
        );

        string playlist = _gen.GenerateMasterPlaylist(plan, MediaTitle);

        playlist.Should().NotContain("TYPE=SUBTITLES");
    }

    // ------------------------------------------------------------------
    // Audio language appears in playlist
    // ------------------------------------------------------------------

    [Fact]
    public void MasterPlaylist_AudioLanguage_Correct()
    {
        OutputPlan plan = CreatePlanWithoutSubtitles();
        string playlist = _gen.GenerateMasterPlaylist(plan, MediaTitle);

        playlist.Should().Contain("LANGUAGE=\"eng\"");
    }

    // ------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------

    private static OutputPlan CreatePlanWithoutSubtitles()
    {
        return new OutputPlan(
            Format: OutputFormat.Hls,
            VideoOutputs: [BuildVideo()],
            AudioOutputs: [BuildAudio()],
            SubtitleOutputs: [],
            Thumbnails: null
        );
    }

    private static VideoOutputPlan BuildVideo() =>
        new(
            Width: 1920,
            Height: 1080,
            EncoderName: "libx264",
            Crf: 23,
            BitrateKbps: 4000,
            Preset: "medium",
            Profile: "high",
            Level: "4.1",
            TenBit: false,
            PixelFormat: "yuv420p",
            MapLabel: "[v0]",
            ExtraFlags: new Dictionary<string, string>()
        );

    private static AudioOutputPlan BuildAudio() =>
        new(
            EncoderName: "aac",
            BitrateKbps: 192,
            Channels: 2,
            SampleRate: 48000,
            Action: StreamAction.Transcode,
            Language: "eng",
            MapLabel: "0:a:0"
        );
}
