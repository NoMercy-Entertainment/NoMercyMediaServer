namespace NoMercy.Tests.Encoder.Output;

using NoMercy.Encoder.Codecs;
using NoMercy.Encoder.Output;
using NoMercy.Encoder.Pipeline;

public class PlaylistGeneratorSubtitleTests
{
    private readonly PlaylistGenerator _gen = new();

    // ------------------------------------------------------------------
    // Plan with subtitles includes EXT-X-MEDIA:TYPE=SUBTITLES
    // ------------------------------------------------------------------

    [Fact]
    public void MasterPlaylist_WithSubtitles_IncludesSubtitleMediaTag()
    {
        OutputPlan plan = CreatePlanWithSubtitles();
        string playlist = _gen.GenerateMasterPlaylist(plan);

        playlist.Should().Contain("#EXT-X-MEDIA:TYPE=SUBTITLES");
    }

    // ------------------------------------------------------------------
    // Subtitle GROUP-ID is "subs"
    // ------------------------------------------------------------------

    [Fact]
    public void MasterPlaylist_WithSubtitles_GroupIdIsSubs()
    {
        OutputPlan plan = CreatePlanWithSubtitles();
        string playlist = _gen.GenerateMasterPlaylist(plan);

        playlist.Should().Contain("GROUP-ID=\"subs\"");
    }

    // ------------------------------------------------------------------
    // Subtitle language appears in NAME and LANGUAGE attributes
    // ------------------------------------------------------------------

    [Fact]
    public void MasterPlaylist_WithSubtitles_LanguageInNameAndLanguageAttributes()
    {
        OutputPlan plan = CreatePlanWithSubtitles("eng");
        string playlist = _gen.GenerateMasterPlaylist(plan);

        playlist.Should().Contain("NAME=\"eng\"");
        playlist.Should().Contain("LANGUAGE=\"eng\"");
    }

    // ------------------------------------------------------------------
    // First subtitle is DEFAULT=YES
    // ------------------------------------------------------------------

    [Fact]
    public void MasterPlaylist_FirstSubtitle_IsDefault()
    {
        OutputPlan plan = CreatePlanWithSubtitles();
        string playlist = _gen.GenerateMasterPlaylist(plan);

        // First subtitle entry should have DEFAULT=YES
        int subtitleIdx = playlist.IndexOf("#EXT-X-MEDIA:TYPE=SUBTITLES", StringComparison.Ordinal);
        string subtitleLine = playlist[subtitleIdx..].Split('\n')[0];
        subtitleLine.Should().Contain("DEFAULT=YES");
    }

    // ------------------------------------------------------------------
    // Second subtitle is DEFAULT=NO
    // ------------------------------------------------------------------

    [Fact]
    public void MasterPlaylist_SecondSubtitle_IsNotDefault()
    {
        OutputPlan plan = CreatePlanWithTwoSubtitles();
        string playlist = _gen.GenerateMasterPlaylist(plan);

        // Split into EXT-X-MEDIA SUBTITLES lines
        string[] lines = playlist.Split('\n');
        string[] subtitleLines = lines.Where(l => l.Contains("TYPE=SUBTITLES")).ToArray();

        subtitleLines.Should().HaveCount(2);
        subtitleLines[1].Should().Contain("DEFAULT=NO");
    }

    // ------------------------------------------------------------------
    // Subtitle URI matches extraction filename convention
    // ------------------------------------------------------------------

    [Fact]
    public void MasterPlaylist_SubtitleUri_MatchesExtractionFilename()
    {
        OutputPlan plan = CreatePlanWithSubtitles("eng");
        string playlist = _gen.GenerateMasterPlaylist(plan);

        // SubtitleExtractor.ResolveOutputFilename produces "subtitles_eng_vtt.vtt"
        playlist.Should().Contain("URI=\"subtitles_eng_vtt.vtt\"");
    }

    // ------------------------------------------------------------------
    // STREAM-INF includes SUBTITLES="subs" when subtitles are present
    // ------------------------------------------------------------------

    [Fact]
    public void MasterPlaylist_WithSubtitles_StreamInfIncludesSubtitleGroup()
    {
        OutputPlan plan = CreatePlanWithSubtitles();
        string playlist = _gen.GenerateMasterPlaylist(plan);

        playlist.Should().Contain("SUBTITLES=\"subs\"");
    }

    // ------------------------------------------------------------------
    // STREAM-INF does NOT include SUBTITLES when no subtitles in plan
    // ------------------------------------------------------------------

    [Fact]
    public void MasterPlaylist_WithoutSubtitles_StreamInfNoSubtitleGroup()
    {
        OutputPlan plan = CreatePlanWithoutSubtitles();
        string playlist = _gen.GenerateMasterPlaylist(plan);

        playlist.Should().NotContain("SUBTITLES=");
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

        string playlist = _gen.GenerateMasterPlaylist(plan);

        playlist.Should().NotContain("TYPE=SUBTITLES");
        playlist.Should().NotContain("GROUP-ID=\"subs\"");
    }

    // ------------------------------------------------------------------
    // Multiple subtitle languages all appear
    // ------------------------------------------------------------------

    [Fact]
    public void MasterPlaylist_TwoSubtitleLanguages_BothAppearInPlaylist()
    {
        OutputPlan plan = CreatePlanWithTwoSubtitles();
        string playlist = _gen.GenerateMasterPlaylist(plan);

        playlist.Should().Contain("LANGUAGE=\"eng\"");
        playlist.Should().Contain("LANGUAGE=\"fra\"");
    }

    // ------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------

    private static OutputPlan CreatePlanWithSubtitles(string language = "eng")
    {
        return new OutputPlan(
            Format: OutputFormat.Hls,
            VideoOutputs: [BuildVideo()],
            AudioOutputs: [BuildAudio()],
            SubtitleOutputs:
            [
                new SubtitleOutputPlan(
                    OutputCodec: SubtitleCodecType.WebVtt,
                    Action: StreamAction.Extract,
                    Language: language,
                    SourceIndex: 0,
                    MapLabel: "0:s:0"
                ),
            ],
            Thumbnails: null
        );
    }

    private static OutputPlan CreatePlanWithTwoSubtitles()
    {
        return new OutputPlan(
            Format: OutputFormat.Hls,
            VideoOutputs: [BuildVideo()],
            AudioOutputs: [BuildAudio()],
            SubtitleOutputs:
            [
                new SubtitleOutputPlan(
                    OutputCodec: SubtitleCodecType.WebVtt,
                    Action: StreamAction.Extract,
                    Language: "eng",
                    SourceIndex: 0,
                    MapLabel: "0:s:0"
                ),
                new SubtitleOutputPlan(
                    OutputCodec: SubtitleCodecType.WebVtt,
                    Action: StreamAction.Extract,
                    Language: "fra",
                    SourceIndex: 1,
                    MapLabel: "0:s:1"
                ),
            ],
            Thumbnails: null
        );
    }

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
