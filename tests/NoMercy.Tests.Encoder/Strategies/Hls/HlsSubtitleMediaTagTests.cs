namespace NoMercy.Tests.Encoder.Strategies.Hls;

using NoMercy.Encoder.Codecs;
using NoMercy.Encoder.Output;
using NoMercy.Encoder.Pipeline;
using NoMercy.Encoder.Subtitles;

/// <summary>
/// Verifies that the master playlist correctly emits EXT-X-MEDIA TYPE=SUBTITLES
/// tags and wires SUBTITLES="subs" onto each EXT-X-STREAM-INF line.
/// </summary>
public class HlsSubtitleMediaTagTests
{
    private static readonly Dictionary<
        string,
        HlsVariantAnalyzer.VariantMetrics
    > EmptyVideoMetrics = [];

    private static readonly Dictionary<
        string,
        HlsVariantAnalyzer.VariantMetrics
    > EmptyAudioMetrics = [];

    private static string GenerateMaster(OutputPlan plan)
    {
        PlaylistGenerator gen = new();
        return gen.GenerateMasterPlaylist(plan, "Test.Movie", EmptyVideoMetrics, EmptyAudioMetrics);
    }

    // ----------------------------------------------------------------
    // One EXT-X-MEDIA per subtitle stream
    // ----------------------------------------------------------------

    [Fact]
    public void MasterPlaylist_HasOneExtXMediaPerSubtitleStream()
    {
        OutputPlan plan = BuildPlan([
            Sub(SubtitleCodecType.WebVtt, "eng"),
            Sub(SubtitleCodecType.WebVtt, "fra"),
        ]);

        string playlist = GenerateMaster(plan);

        int tagCount = CountOccurrences(playlist, "TYPE=SUBTITLES");
        tagCount.Should().Be(2, "two subtitle streams → two EXT-X-MEDIA tags");
    }

    // ----------------------------------------------------------------
    // STREAM-INF includes SUBTITLES="subs" when subtitles present
    // ----------------------------------------------------------------

    [Fact]
    public void StreamInf_IncludesSubtitlesAttribute_WhenSubtitlesPresentAll()
    {
        OutputPlan plan = BuildPlan([Sub(SubtitleCodecType.WebVtt, "eng")]);

        string playlist = GenerateMaster(plan);

        playlist.Should().Contain("SUBTITLES=\"subs\"");
    }

    [Fact]
    public void StreamInf_NoSubtitlesAttribute_WhenNoSubtitles()
    {
        OutputPlan plan = BuildPlan([]);

        string playlist = GenerateMaster(plan);

        playlist.Should().NotContain("SUBTITLES=");
    }

    // ----------------------------------------------------------------
    // 3-source fixture: SRT + PGS (dropped) + ASS → 2 active EXT-X-MEDIA
    // ----------------------------------------------------------------

    [Fact]
    public void ThreeSourceFixture_SrtPgsAss_EmitsCorrectMediaTags()
    {
        // PGS with Drop action should NOT appear; SRT and ASS (Extract) should.
        SubtitleOutputPlan srt = new(
            OutputCodec: SubtitleCodecType.WebVtt,
            Action: StreamAction.Extract,
            Language: "eng",
            SourceIndex: 0,
            MapLabel: "0:s:0"
        );

        SubtitleOutputPlan pgs = new(
            OutputCodec: SubtitleCodecType.Pgs,
            Action: StreamAction.Drop,
            Language: "eng",
            SourceIndex: 1,
            MapLabel: "0:s:1"
        );

        SubtitleOutputPlan ass = new(
            OutputCodec: SubtitleCodecType.Ass,
            Action: StreamAction.Extract,
            Language: "fra",
            SourceIndex: 2,
            MapLabel: "0:s:2"
        );

        OutputPlan plan = BuildPlan([srt, pgs, ass]);

        string playlist = GenerateMaster(plan);

        int tagCount = CountOccurrences(playlist, "TYPE=SUBTITLES");
        tagCount.Should().Be(2, "SRT and ASS are active; PGS is dropped and must not appear");

        playlist.Should().Contain("LANGUAGE=\"fra\"");
        playlist.Should().Contain("LANGUAGE=\"eng\"");
    }

    // ----------------------------------------------------------------
    // WebVttSegmenter media playlist — font copy scenario (unit)
    // ----------------------------------------------------------------

    [Fact]
    public void SubtitleMediaPlaylist_FontCopy_SegmentsReferenceCorrectUris()
    {
        // Simulate font files alongside .ass sidecar: the playlist generator
        // emits one entry per segment. Font presence is verified by the caller
        // copying font files to the output dir; the playlist itself doesn't
        // list fonts. This test validates the ASS playlist format.
        SubtitleOutputPlan sub = new(
            OutputCodec: SubtitleCodecType.Ass,
            Action: StreamAction.Extract,
            Language: "eng",
            SourceIndex: 0,
            MapLabel: "0:s:0"
        );

        string playlist = PlaylistGenerator.GenerateAssMediaPlaylist(sub, "subs_eng.ass", 6);

        playlist.Should().StartWith("#EXTM3U");
        playlist.Should().Contain("subs_eng.ass");
        playlist.Should().Contain("#EXT-X-ENDLIST");
        playlist.Should().Contain("#EXT-X-TARGETDURATION:6");
    }

    // ----------------------------------------------------------------
    // WebVttSegmenter media playlist for VTT segments
    // ----------------------------------------------------------------

    [Fact]
    public void SubtitleMediaPlaylist_VttSegments_ContainsCorrectEntries()
    {
        WebVttSegmenter segmenter = new();
        string vttContent =
            "WEBVTT\n\n00:00:01.000 --> 00:00:03.000\nHello\n\n"
            + "00:00:07.000 --> 00:00:08.000\nWorld\n";

        IReadOnlyList<WebVttSegment> segments = segmenter.SliceContent(
            vttContent,
            TimeSpan.FromSeconds(6)
        );

        SubtitleOutputPlan sub = new(
            OutputCodec: SubtitleCodecType.WebVtt,
            Action: StreamAction.Extract,
            Language: "eng",
            SourceIndex: 0,
            MapLabel: "0:s:0"
        );

        string playlist = PlaylistGenerator.GenerateSubtitleMediaPlaylist(sub, segments, 6);

        playlist.Should().Contain("#EXTM3U");
        playlist.Should().Contain("subs_eng_00000.vtt");
        playlist.Should().Contain("subs_eng_00001.vtt");
        playlist.Should().Contain("#EXT-X-ENDLIST");
    }

    // ----------------------------------------------------------------
    // Helpers
    // ----------------------------------------------------------------

    private static OutputPlan BuildPlan(SubtitleOutputPlan[] subs) =>
        new(
            Format: OutputFormat.Hls,
            VideoOutputs: [BuildVideo()],
            AudioOutputs: [BuildAudio()],
            SubtitleOutputs: subs,
            Thumbnails: null
        );

    private static SubtitleOutputPlan Sub(SubtitleCodecType codec, string lang) =>
        new(
            OutputCodec: codec,
            Action: StreamAction.Extract,
            Language: lang,
            SourceIndex: 0,
            MapLabel: "0:s:0"
        );

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

    private static int CountOccurrences(string haystack, string needle)
    {
        int count = 0;
        int idx = 0;
        while ((idx = haystack.IndexOf(needle, idx, StringComparison.Ordinal)) >= 0)
        {
            count++;
            idx += needle.Length;
        }

        return count;
    }
}
