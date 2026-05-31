using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NoMercy.Encoder.Analysis;
using NoMercy.Encoder.Codecs;
using NoMercy.Encoder.Composition;
using NoMercy.Encoder.Subscribers;
using NoMercy.Encoder.Subtitles;
using NoMercy.Events;
using NoMercy.Events.Encoding;

namespace NoMercy.Tests.Encoder.Subscribers;

/// <summary>
/// Branch-coverage gaps for <see cref="OcrPostEncodeSubscriber"/>:
///
/// • Null OCR engine — when no <see cref="ISubtitleOcrEngine"/> is registered,
///   the subscriber logs and returns without probing. Same as disabled, but
///   reached via a different code path (DI vs config).
/// • Analyzer probe failure — exception caught, logged, never bubbles up
///   (would otherwise fail an already-completed encode).
/// • Per-stream OCR failure — one bad stream must not abort the loop;
///   subsequent streams still get OCR attempts.
/// • Multiple bitmap streams — OCR fires once per bitmap stream with the
///   correct stream index.
/// • Mixed bitmap + text streams — OCR only fires on bitmap streams.
/// • Language null fallback — defaults to "eng" so the OCR engine has a
///   model to pick.
/// • All bitmap codec spellings — pgs / hdmv_pgs_subtitle / dvd_subtitle /
///   vobsub / dvb_subtitle (case-insensitive).
/// • IsStreamingOutput — .mpd / .M3U8 case mix accepted; non-streaming
///   extensions rejected.
/// </summary>
public class OcrPostEncodeSubscriberBranchTests
{
    private static MediaInfo Info(params (string Codec, string? Language)[] subtitleStreams)
    {
        List<SubtitleStreamInfo> subs = subtitleStreams
            .Select(
                (s, idx) =>
                    new SubtitleStreamInfo(
                        Index: idx,
                        Codec: s.Codec,
                        Language: s.Language,
                        IsDefault: false,
                        IsForced: false
                    )
            )
            .ToList();

        return new MediaInfo(
            FilePath: "/out/master.m3u8",
            Format: "mpegts",
            Duration: TimeSpan.FromMinutes(60),
            OverallBitRateKbps: 0,
            FileSizeBytes: 0,
            VideoStreams: [],
            AudioStreams: [],
            SubtitleStreams: subs,
            Chapters: []
        );
    }

    private static OcrPostEncodeSubscriber Build(
        EncoderOptions options,
        Mock<IMediaAnalyzer> analyzer,
        Mock<ISubtitleOcrEngine>? ocr = null
    ) =>
        new(
            new Mock<IEventBus>().Object,
            analyzer.Object,
            options,
            NullLogger<OcrPostEncodeSubscriber>.Instance,
            ocr?.Object
        );

    private static EncodingCompletedEvent Event(string outputPath = "/out/master.m3u8") =>
        new()
        {
            JobId = 1,
            OutputPath = outputPath,
            Duration = TimeSpan.FromMinutes(60),
        };

    // ── Null OCR engine ──────────────────────────────────────────────────────

    [Fact]
    public async Task Null_ocr_engine_skips_without_probing()
    {
        Mock<IMediaAnalyzer> analyzer = new();
        OcrPostEncodeSubscriber subscriber = Build(new EncoderOptions(), analyzer, ocr: null);

        await subscriber.OnEncodingCompleted(Event(), CancellationToken.None);

        analyzer.VerifyNoOtherCalls();
    }

    // ── Analyzer failure ─────────────────────────────────────────────────────

    [Fact]
    public async Task Analyzer_throws_is_swallowed_and_skips_OCR()
    {
        Mock<IMediaAnalyzer> analyzer = new();
        analyzer
            .Setup(a => a.AnalyzeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("probe boom"));

        Mock<ISubtitleOcrEngine> ocr = new();
        OcrPostEncodeSubscriber subscriber = Build(new EncoderOptions(), analyzer, ocr);

        Func<Task> act = () => subscriber.OnEncodingCompleted(Event(), CancellationToken.None);
        await act.Should().NotThrowAsync();

        ocr.VerifyNoOtherCalls();
    }

    // ── Per-stream OCR failure ──────────────────────────────────────────────

    [Fact]
    public async Task OCR_failure_on_one_stream_does_not_abort_subsequent_streams()
    {
        Mock<IMediaAnalyzer> analyzer = new();
        analyzer
            .Setup(a => a.AnalyzeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Info(("pgs", "eng"), ("pgs", "fre")));

        Mock<ISubtitleOcrEngine> ocr = new();
        // First call (index 0) throws; second call (index 1) succeeds.
        ocr.Setup(o =>
                o.OcrAsync(
                    It.IsAny<string>(),
                    0,
                    It.IsAny<string>(),
                    It.IsAny<SubtitleCodecType>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ThrowsAsync(new IOException("disk full"));

        ocr.Setup(o =>
                o.OcrAsync(
                    It.IsAny<string>(),
                    1,
                    It.IsAny<string>(),
                    It.IsAny<SubtitleCodecType>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                new SubtitleTrack("/out/sub.fre.vtt", "fre", SubtitleCodecType.WebVtt, 5)
            );

        OcrPostEncodeSubscriber subscriber = Build(new EncoderOptions(), analyzer, ocr);

        Func<Task> act = () => subscriber.OnEncodingCompleted(Event(), CancellationToken.None);
        await act.Should().NotThrowAsync();

        // Both streams attempted — failure on first does not block second.
        ocr.Verify(
            o =>
                o.OcrAsync(
                    It.IsAny<string>(),
                    0,
                    It.IsAny<string>(),
                    It.IsAny<SubtitleCodecType>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );
        ocr.Verify(
            o =>
                o.OcrAsync(
                    It.IsAny<string>(),
                    1,
                    It.IsAny<string>(),
                    It.IsAny<SubtitleCodecType>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );
    }

    // ── Multiple bitmap streams ──────────────────────────────────────────────

    [Fact]
    public async Task Multiple_bitmap_streams_each_get_separate_OCR_call_with_correct_index()
    {
        Mock<IMediaAnalyzer> analyzer = new();
        analyzer
            .Setup(a => a.AnalyzeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                Info(
                    ("hdmv_pgs_subtitle", "eng"),
                    ("hdmv_pgs_subtitle", "fre"),
                    ("hdmv_pgs_subtitle", "ger")
                )
            );

        Mock<ISubtitleOcrEngine> ocr = new();
        ocr.Setup(o =>
                o.OcrAsync(
                    It.IsAny<string>(),
                    It.IsAny<int>(),
                    It.IsAny<string>(),
                    It.IsAny<SubtitleCodecType>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(new SubtitleTrack("/out/sub.vtt", "eng", SubtitleCodecType.WebVtt, 5));

        OcrPostEncodeSubscriber subscriber = Build(new EncoderOptions(), analyzer, ocr);

        await subscriber.OnEncodingCompleted(Event(), CancellationToken.None);

        ocr.Verify(
            o =>
                o.OcrAsync(
                    It.IsAny<string>(),
                    0,
                    "eng",
                    SubtitleCodecType.WebVtt,
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );
        ocr.Verify(
            o =>
                o.OcrAsync(
                    It.IsAny<string>(),
                    1,
                    "fre",
                    SubtitleCodecType.WebVtt,
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );
        ocr.Verify(
            o =>
                o.OcrAsync(
                    It.IsAny<string>(),
                    2,
                    "ger",
                    SubtitleCodecType.WebVtt,
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );
    }

    // ── Mixed bitmap + text streams ─────────────────────────────────────────

    [Fact]
    public async Task Mixed_streams_only_bitmap_codecs_get_OCR()
    {
        Mock<IMediaAnalyzer> analyzer = new();
        analyzer
            .Setup(a => a.AnalyzeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                Info(
                    ("subrip", "eng"), // text → skip
                    ("hdmv_pgs_subtitle", "eng"), // bitmap → OCR
                    ("ass", "fre"), // text → skip
                    ("dvd_subtitle", "ger") // bitmap → OCR
                )
            );

        Mock<ISubtitleOcrEngine> ocr = new();
        ocr.Setup(o =>
                o.OcrAsync(
                    It.IsAny<string>(),
                    It.IsAny<int>(),
                    It.IsAny<string>(),
                    It.IsAny<SubtitleCodecType>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(new SubtitleTrack("/out/sub.vtt", "eng", SubtitleCodecType.WebVtt, 5));

        OcrPostEncodeSubscriber subscriber = Build(new EncoderOptions(), analyzer, ocr);

        await subscriber.OnEncodingCompleted(Event(), CancellationToken.None);

        // 2 OCR calls — for indices 1 and 3.
        ocr.Verify(
            o =>
                o.OcrAsync(
                    It.IsAny<string>(),
                    It.IsAny<int>(),
                    It.IsAny<string>(),
                    It.IsAny<SubtitleCodecType>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Exactly(2)
        );
        ocr.Verify(
            o =>
                o.OcrAsync(
                    It.IsAny<string>(),
                    1,
                    It.IsAny<string>(),
                    It.IsAny<SubtitleCodecType>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );
        ocr.Verify(
            o =>
                o.OcrAsync(
                    It.IsAny<string>(),
                    3,
                    It.IsAny<string>(),
                    It.IsAny<SubtitleCodecType>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );
    }

    // ── Language null fallback ───────────────────────────────────────────────

    [Fact]
    public async Task Null_language_falls_back_to_eng_for_OCR()
    {
        Mock<IMediaAnalyzer> analyzer = new();
        analyzer
            .Setup(a => a.AnalyzeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Info(("pgs", null)));

        Mock<ISubtitleOcrEngine> ocr = new();
        ocr.Setup(o =>
                o.OcrAsync(
                    It.IsAny<string>(),
                    It.IsAny<int>(),
                    It.IsAny<string>(),
                    It.IsAny<SubtitleCodecType>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(new SubtitleTrack("/out/sub.vtt", "eng", SubtitleCodecType.WebVtt, 5));

        OcrPostEncodeSubscriber subscriber = Build(new EncoderOptions(), analyzer, ocr);

        await subscriber.OnEncodingCompleted(Event(), CancellationToken.None);

        ocr.Verify(
            o =>
                o.OcrAsync(
                    It.IsAny<string>(),
                    0,
                    "eng",
                    SubtitleCodecType.WebVtt,
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );
    }

    // ── All bitmap codec spellings ──────────────────────────────────────────

    [Theory]
    [InlineData("hdmv_pgs_subtitle")]
    [InlineData("pgs")]
    [InlineData("dvd_subtitle")]
    [InlineData("vobsub")]
    [InlineData("dvb_subtitle")]
    public async Task Each_bitmap_codec_spelling_triggers_OCR(string codec)
    {
        Mock<IMediaAnalyzer> analyzer = new();
        analyzer
            .Setup(a => a.AnalyzeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Info((codec, "eng")));

        Mock<ISubtitleOcrEngine> ocr = new();
        ocr.Setup(o =>
                o.OcrAsync(
                    It.IsAny<string>(),
                    It.IsAny<int>(),
                    It.IsAny<string>(),
                    It.IsAny<SubtitleCodecType>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(new SubtitleTrack("/out/sub.vtt", "eng", SubtitleCodecType.WebVtt, 5));

        OcrPostEncodeSubscriber subscriber = Build(new EncoderOptions(), analyzer, ocr);

        await subscriber.OnEncodingCompleted(Event(), CancellationToken.None);

        ocr.Verify(
            o =>
                o.OcrAsync(
                    It.IsAny<string>(),
                    It.IsAny<int>(),
                    It.IsAny<string>(),
                    It.IsAny<SubtitleCodecType>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );
    }

    [Theory]
    [InlineData("HDMV_PGS_SUBTITLE")]
    [InlineData("Pgs")]
    [InlineData("DVD_Subtitle")]
    public async Task Bitmap_codec_match_is_case_insensitive(string codec)
    {
        Mock<IMediaAnalyzer> analyzer = new();
        analyzer
            .Setup(a => a.AnalyzeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Info((codec, "eng")));

        Mock<ISubtitleOcrEngine> ocr = new();
        ocr.Setup(o =>
                o.OcrAsync(
                    It.IsAny<string>(),
                    It.IsAny<int>(),
                    It.IsAny<string>(),
                    It.IsAny<SubtitleCodecType>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(new SubtitleTrack("/out/sub.vtt", "eng", SubtitleCodecType.WebVtt, 5));

        OcrPostEncodeSubscriber subscriber = Build(new EncoderOptions(), analyzer, ocr);

        await subscriber.OnEncodingCompleted(Event(), CancellationToken.None);

        ocr.Verify(
            o =>
                o.OcrAsync(
                    It.IsAny<string>(),
                    It.IsAny<int>(),
                    It.IsAny<string>(),
                    It.IsAny<SubtitleCodecType>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );
    }

    // ── IsStreamingOutput extension matching ────────────────────────────────

    [Theory]
    [InlineData("/out/master.M3U8")]
    [InlineData("/out/master.m3u8")]
    [InlineData("/out/playlist.mpd")]
    [InlineData("/out/playlist.MPD")]
    public async Task Streaming_extensions_trigger_OCR_path(string outputPath)
    {
        Mock<IMediaAnalyzer> analyzer = new();
        analyzer
            .Setup(a => a.AnalyzeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Info(("pgs", "eng")));

        Mock<ISubtitleOcrEngine> ocr = new();
        ocr.Setup(o =>
                o.OcrAsync(
                    It.IsAny<string>(),
                    It.IsAny<int>(),
                    It.IsAny<string>(),
                    It.IsAny<SubtitleCodecType>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(new SubtitleTrack("/out/sub.vtt", "eng", SubtitleCodecType.WebVtt, 5));

        OcrPostEncodeSubscriber subscriber = Build(new EncoderOptions(), analyzer, ocr);

        await subscriber.OnEncodingCompleted(Event(outputPath), CancellationToken.None);

        analyzer.Verify(a => a.AnalyzeAsync(outputPath, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData("/out/movie.mp4")]
    [InlineData("/out/movie.mkv")]
    [InlineData("/out/audio.flac")]
    [InlineData("/out/no-extension")]
    public async Task Non_streaming_outputs_skip_without_probing(string outputPath)
    {
        Mock<IMediaAnalyzer> analyzer = new();
        Mock<ISubtitleOcrEngine> ocr = new();
        OcrPostEncodeSubscriber subscriber = Build(new EncoderOptions(), analyzer, ocr);

        await subscriber.OnEncodingCompleted(Event(outputPath), CancellationToken.None);

        analyzer.VerifyNoOtherCalls();
        ocr.VerifyNoOtherCalls();
    }
}
