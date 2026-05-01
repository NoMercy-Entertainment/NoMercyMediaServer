using NoMercy.Encoder.Codecs;
using NoMercy.Encoder.Profiles;
using NoMercy.Encoder.Subtitles;

namespace NoMercy.Tests.Encoder.Subtitles;

public class SubtitleRouterTests
{
    private readonly SubtitleRouter _router = new();

    public static TheoryData<
        SubtitleSourceType,
        OutputFormat,
        SubtitleMode,
        SubtitleAction
    > SpecMatrix =>
        new()
        {
            // text → mkv → copy regardless of mode
            {
                SubtitleSourceType.Text,
                OutputFormat.Mkv,
                SubtitleMode.Extract,
                SubtitleAction.Copy
            },
            {
                SubtitleSourceType.Text,
                OutputFormat.Mkv,
                SubtitleMode.PassThrough,
                SubtitleAction.Copy
            },
            // text → hls → extract → vtt (segmented)
            {
                SubtitleSourceType.Text,
                OutputFormat.Hls,
                SubtitleMode.Extract,
                SubtitleAction.ExtractVtt
            },
            // text → mp4 → extract → mov_text (embedded). PassThrough → sidecar.
            {
                SubtitleSourceType.Text,
                OutputFormat.Mp4,
                SubtitleMode.Extract,
                SubtitleAction.MovText
            },
            {
                SubtitleSourceType.Text,
                OutputFormat.Mp4,
                SubtitleMode.PassThrough,
                SubtitleAction.ExtractVttSidecar
            },
            // text → dash → vtt sidecar
            {
                SubtitleSourceType.Text,
                OutputFormat.Dash,
                SubtitleMode.Extract,
                SubtitleAction.ExtractVttSidecar
            },
            // bitmap → mkv → copy
            {
                SubtitleSourceType.Bitmap,
                OutputFormat.Mkv,
                SubtitleMode.Extract,
                SubtitleAction.Copy
            },
            // bitmap → hls → ocr (or BurnIn when explicit)
            {
                SubtitleSourceType.Bitmap,
                OutputFormat.Hls,
                SubtitleMode.Extract,
                SubtitleAction.Ocr
            },
            {
                SubtitleSourceType.Bitmap,
                OutputFormat.Hls,
                SubtitleMode.BurnIn,
                SubtitleAction.BurnIn
            },
            // bitmap → mp4 → ocr sidecar (or BurnIn)
            {
                SubtitleSourceType.Bitmap,
                OutputFormat.Mp4,
                SubtitleMode.Extract,
                SubtitleAction.OcrSidecar
            },
            {
                SubtitleSourceType.Bitmap,
                OutputFormat.Mp4,
                SubtitleMode.BurnIn,
                SubtitleAction.BurnIn
            },
            // bitmap → dash → ocr sidecar (or BurnIn)
            {
                SubtitleSourceType.Bitmap,
                OutputFormat.Dash,
                SubtitleMode.Extract,
                SubtitleAction.OcrSidecar
            },
            {
                SubtitleSourceType.Bitmap,
                OutputFormat.Dash,
                SubtitleMode.BurnIn,
                SubtitleAction.BurnIn
            },
        };

    [Theory]
    [MemberData(nameof(SpecMatrix))]
    public void Resolve_returns_expected_action(
        SubtitleSourceType source,
        OutputFormat container,
        SubtitleMode mode,
        SubtitleAction expected
    )
    {
        SubtitleRouting routing = _router.Resolve(source, container, mode);
        routing.Action.Should().Be(expected);
    }

    [Theory]
    [InlineData(OutputFormat.Mp3)]
    [InlineData(OutputFormat.Flac)]
    [InlineData(OutputFormat.Ogg)]
    public void Resolve_audio_only_container_returns_None_with_reason(OutputFormat container)
    {
        SubtitleRouting textRouting = _router.Resolve(
            SubtitleSourceType.Text,
            container,
            SubtitleMode.Extract
        );
        SubtitleRouting bitmapRouting = _router.Resolve(
            SubtitleSourceType.Bitmap,
            container,
            SubtitleMode.BurnIn
        );

        textRouting.Action.Should().Be(SubtitleAction.None);
        textRouting.Reason.Should().Contain("no subtitle support");

        // BurnIn doesn't sneak past the audio-only guard.
        bitmapRouting.Action.Should().Be(SubtitleAction.None);
    }

    [Fact]
    public void Resolve_BurnIn_overrides_source_type_when_container_supports_video()
    {
        // BurnIn is mode-driven, not source-driven — text + hls + BurnIn still burns.
        SubtitleRouting routing = _router.Resolve(
            SubtitleSourceType.Text,
            OutputFormat.Hls,
            SubtitleMode.BurnIn
        );
        routing.Action.Should().Be(SubtitleAction.BurnIn);
    }
}
