namespace NoMercy.Tests.Encoder.Output;

using NoMercy.Encoder.Codecs;
using NoMercy.Encoder.Output;
using NoMercy.Encoder.Pipeline;

public class PlaylistGeneratorSubtitleTests
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
    public void MasterPlaylist_WithoutSubtitles_NoSubtitleTags()
    {
        string playlist = Generate(CreatePlanWithoutSubtitles());

        playlist.Should().NotContain("TYPE=SUBTITLES");
    }

    [Fact]
    public void MasterPlaylist_SubtitleWithDropAction_IsExcluded()
    {
        OutputPlan plan = new(
            Format: OutputFormat.Hls,
            VideoOutputs: [BuildVideo()],
            AudioOutputs: [BuildAudio()],
            SubtitleOutputs:
            [
                new(
                    OutputCodec: SubtitleCodecType.WebVtt,
                    Action: StreamAction.Drop,
                    Language: "eng",
                    SourceIndex: 0,
                    MapLabel: "0:s:0"
                ),
            ],
            Thumbnails: null
        );

        string playlist = Generate(plan);

        playlist.Should().NotContain("TYPE=SUBTITLES");
    }

    [Fact]
    public void MasterPlaylist_AudioLanguage_Correct()
    {
        string playlist = Generate(CreatePlanWithoutSubtitles());

        playlist.Should().Contain("LANGUAGE=\"eng\"");
    }

    private static OutputPlan CreatePlanWithoutSubtitles()
    {
        return new(
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
            ExtraFlags: new()
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
