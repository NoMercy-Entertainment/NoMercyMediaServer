namespace NoMercy.Tests.Encoder.PostProcess;

using NoMercy.Encoder.Analysis;
using NoMercy.Encoder.Codecs;
using NoMercy.Encoder.Commands;
using NoMercy.Encoder.Output;
using NoMercy.Encoder.Pipeline;
using NoMercy.Encoder.PostProcess;

public class SubtitleExtractorTests
{
    private readonly SubtitleExtractor _extractor = new();

    // ------------------------------------------------------------------
    // WebVTT extraction produces correct output filename and codec
    // ------------------------------------------------------------------

    [Fact]
    public void BuildExtractionCommands_WebVtt_CorrectCodecAndFilename()
    {
        SubtitleStreamInfo[] streams =
        [
            new SubtitleStreamInfo(
                Index: 0,
                Codec: "subrip",
                Language: "eng",
                IsDefault: true,
                IsForced: false
            ),
        ];

        SubtitleOutputPlan[] plans =
        [
            new SubtitleOutputPlan(
                OutputCodec: SubtitleCodecType.WebVtt,
                Action: StreamAction.Extract,
                Language: "eng",
                SourceIndex: 0,
                MapLabel: "0:s:0"
            ),
        ];

        FfmpegCommand[] commands = _extractor.BuildExtractionCommands(
            "ffmpeg",
            "/input/movie.mkv",
            "/output",
            streams,
            plans
        );

        commands.Should().HaveCount(1);
        commands[0].Arguments.Should().Contain("webvtt");
        commands[0].Arguments.Should().Contain(Path.Combine("/output", "subtitles_eng_vtt.vtt"));
        commands[0].Arguments.Should().Contain("0:s:0");
    }

    // ------------------------------------------------------------------
    // SRT extraction uses srt codec
    // ------------------------------------------------------------------

    [Fact]
    public void BuildExtractionCommands_Srt_UsesSrtCodec()
    {
        SubtitleStreamInfo[] streams =
        [
            new SubtitleStreamInfo(
                Index: 0,
                Codec: "subrip",
                Language: "fra",
                IsDefault: false,
                IsForced: false
            ),
        ];

        SubtitleOutputPlan[] plans =
        [
            new SubtitleOutputPlan(
                OutputCodec: SubtitleCodecType.Srt,
                Action: StreamAction.Extract,
                Language: "fra",
                SourceIndex: 0,
                MapLabel: "0:s:0"
            ),
        ];

        FfmpegCommand[] commands = _extractor.BuildExtractionCommands(
            "ffmpeg",
            "/input/movie.mkv",
            "/output",
            streams,
            plans
        );

        commands.Should().HaveCount(1);
        commands[0].Arguments.Should().Contain("srt");
        commands[0].Arguments.Should().Contain(Path.Combine("/output", "subtitles_fra_srt.srt"));
    }

    // ------------------------------------------------------------------
    // Stream index maps correctly in -map argument
    // ------------------------------------------------------------------

    [Fact]
    public void BuildExtractionCommands_MultipleStreams_MapsCorrectStreamIndex()
    {
        SubtitleStreamInfo[] streams =
        [
            new SubtitleStreamInfo(
                Index: 0,
                Codec: "subrip",
                Language: "eng",
                IsDefault: true,
                IsForced: false
            ),
            new SubtitleStreamInfo(
                Index: 1,
                Codec: "subrip",
                Language: "deu",
                IsDefault: false,
                IsForced: false
            ),
        ];

        SubtitleOutputPlan[] plans =
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
                Language: "deu",
                SourceIndex: 1,
                MapLabel: "0:s:1"
            ),
        ];

        FfmpegCommand[] commands = _extractor.BuildExtractionCommands(
            "ffmpeg",
            "/input/movie.mkv",
            "/output",
            streams,
            plans
        );

        commands.Should().HaveCount(2);
        commands[0].Arguments.Should().Contain("0:s:0");
        commands[1].Arguments.Should().Contain("0:s:1");
    }

    // ------------------------------------------------------------------
    // Drop action is skipped — no command produced
    // ------------------------------------------------------------------

    [Fact]
    public void BuildExtractionCommands_DropAction_ProducesNoCommand()
    {
        SubtitleStreamInfo[] streams =
        [
            new SubtitleStreamInfo(
                Index: 0,
                Codec: "subrip",
                Language: "eng",
                IsDefault: true,
                IsForced: false
            ),
        ];

        SubtitleOutputPlan[] plans =
        [
            new SubtitleOutputPlan(
                OutputCodec: SubtitleCodecType.WebVtt,
                Action: StreamAction.Drop,
                Language: "eng",
                SourceIndex: 0,
                MapLabel: "0:s:0"
            ),
        ];

        FfmpegCommand[] commands = _extractor.BuildExtractionCommands(
            "ffmpeg",
            "/input/movie.mkv",
            "/output",
            streams,
            plans
        );

        commands.Should().BeEmpty();
    }

    // ------------------------------------------------------------------
    // Transcode action is skipped for extraction
    // ------------------------------------------------------------------

    [Fact]
    public void BuildExtractionCommands_TranscodeAction_ProducesNoCommand()
    {
        SubtitleStreamInfo[] streams =
        [
            new SubtitleStreamInfo(
                Index: 0,
                Codec: "dvdsub",
                Language: "eng",
                IsDefault: true,
                IsForced: false
            ),
        ];

        SubtitleOutputPlan[] plans =
        [
            new SubtitleOutputPlan(
                OutputCodec: SubtitleCodecType.WebVtt,
                Action: StreamAction.Transcode,
                Language: "eng",
                SourceIndex: 0,
                MapLabel: "0:s:0"
            ),
        ];

        FfmpegCommand[] commands = _extractor.BuildExtractionCommands(
            "ffmpeg",
            "/input/movie.mkv",
            "/output",
            streams,
            plans
        );

        commands.Should().BeEmpty();
    }

    // ------------------------------------------------------------------
    // Copy action is treated the same as Extract
    // ------------------------------------------------------------------

    [Fact]
    public void BuildExtractionCommands_CopyAction_ProducesCommand()
    {
        SubtitleStreamInfo[] streams =
        [
            new SubtitleStreamInfo(
                Index: 0,
                Codec: "webvtt",
                Language: "eng",
                IsDefault: true,
                IsForced: false
            ),
        ];

        SubtitleOutputPlan[] plans =
        [
            new SubtitleOutputPlan(
                OutputCodec: SubtitleCodecType.WebVtt,
                Action: StreamAction.Copy,
                Language: "eng",
                SourceIndex: 0,
                MapLabel: "0:s:0"
            ),
        ];

        FfmpegCommand[] commands = _extractor.BuildExtractionCommands(
            "ffmpeg",
            "/input/movie.mkv",
            "/output",
            streams,
            plans
        );

        commands.Should().HaveCount(1);
    }

    // ------------------------------------------------------------------
    // Language fallback: plan language used when stream list is shorter
    // ------------------------------------------------------------------

    [Fact]
    public void BuildExtractionCommands_MissingStream_FallsBackToPlanLanguage()
    {
        // Empty streams — plan has a language
        SubtitleOutputPlan[] plans =
        [
            new SubtitleOutputPlan(
                OutputCodec: SubtitleCodecType.WebVtt,
                Action: StreamAction.Extract,
                Language: "jpn",
                SourceIndex: 0,
                MapLabel: "0:s:0"
            ),
        ];

        FfmpegCommand[] commands = _extractor.BuildExtractionCommands(
            "ffmpeg",
            "/input/movie.mkv",
            "/output",
            [],
            plans
        );

        commands.Should().HaveCount(1);
        commands[0].Arguments.Should().Contain(Path.Combine("/output", "subtitles_jpn_vtt.vtt"));
    }

    // ------------------------------------------------------------------
    // ResolveOutputFilename helper
    // ------------------------------------------------------------------

    [Theory]
    [InlineData(SubtitleCodecType.WebVtt, "eng", "subtitles_eng_vtt.vtt")]
    [InlineData(SubtitleCodecType.Srt, "fra", "subtitles_fra_srt.srt")]
    [InlineData(SubtitleCodecType.Ass, "deu", "subtitles_deu_ass.ass")]
    public void ResolveOutputFilename_ReturnsCorrectName(
        SubtitleCodecType codec,
        string language,
        string expected
    )
    {
        SubtitleOutputPlan plan = new(
            OutputCodec: codec,
            Action: StreamAction.Extract,
            Language: language,
            SourceIndex: 0,
            MapLabel: "0:s:0"
        );

        string filename = SubtitleExtractor.ResolveOutputFilename(plan, language);

        filename.Should().Be(expected);
    }
}
