namespace NoMercy.Encoder.DiscRipping;

using System.Text.Json;
using Microsoft.Extensions.Logging;
using NoMercy.Encoder.Analysis;
using NoMercy.Encoder.Composition;
using NoMercy.Encoder.Infrastructure;

/// <summary>
/// Enumerates titles on a Blu-ray or DVD by invoking FFprobe against the
/// disc's libbluray / libdvdread pseudo-URL. Returns a <see cref="DiscInfo"/>
/// with every playable title the user can then select for ripping.
///
/// Drive path conventions:
/// - Linux Blu-ray: <c>bluray:/dev/sr0</c>
/// - Linux DVD:     <c>/dev/sr0</c> (ffprobe auto-detects the VIDEO_TS tree)
/// - Windows:       <c>bluray:D:</c> or <c>D:\</c>
///
/// The caller passes whichever form the OS yielded — the scanner doesn't
/// rewrite paths. <see cref="IDriveMonitor"/> produces usable paths.
/// </summary>
public class DiscScanner(
    EncoderOptions options,
    IProcessRunner processRunner,
    ILogger<DiscScanner> logger
) : IDiscScanner
{
    public async Task<DiscInfo> ScanAsync(string drivePath, CancellationToken ct)
    {
        OpticalDiscType discType = drivePath.StartsWith(
            "bluray:",
            StringComparison.OrdinalIgnoreCase
        )
            ? OpticalDiscType.BluRay
            : OpticalDiscType.Dvd;

        string[] args =
        [
            "-v",
            "quiet",
            "-print_format",
            "json",
            "-show_format",
            "-show_streams",
            "-show_chapters",
            drivePath,
        ];

        ProcessResult result = await processRunner.RunAsync(
            options.FfprobePath,
            args,
            workingDirectory: null,
            cancellationToken: ct
        );

        if (!result.IsSuccess || string.IsNullOrWhiteSpace(result.StdOut))
        {
            logger.LogWarning(
                "Disc scan failed for {Drive} (exit {Exit}): {Stderr}",
                drivePath,
                result.ExitCode,
                TrimStderr(result.StdErr)
            );
            return new DiscInfo(discType, null, [], null, TimeSpan.Zero);
        }

        try
        {
            return Parse(result.StdOut, discType);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to parse disc scan output for {Drive}", drivePath);
            return new DiscInfo(discType, null, [], null, TimeSpan.Zero);
        }
    }

    /// <summary>
    /// Parses the ffprobe JSON envelope. For a Blu-ray, ffprobe's default
    /// output is the "longest" title only — the scanner reports just that
    /// one title for now. Multi-title enumeration requires libbluray's
    /// playlist dump, which is a separate tool-chain step; a V2 can add it.
    /// </summary>
    internal static DiscInfo Parse(string json, OpticalDiscType discType)
    {
        using JsonDocument doc = JsonDocument.Parse(json);
        JsonElement root = doc.RootElement;

        string? discLabel = null;
        TimeSpan duration = TimeSpan.Zero;
        if (root.TryGetProperty("format", out JsonElement format))
        {
            if (
                format.TryGetProperty("tags", out JsonElement tags)
                && tags.TryGetProperty("title", out JsonElement titleElement)
            )
            {
                discLabel = titleElement.GetString();
            }
            if (
                format.TryGetProperty("duration", out JsonElement durationElement)
                && double.TryParse(
                    durationElement.GetString(),
                    System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out double seconds
                )
            )
            {
                duration = TimeSpan.FromSeconds(seconds);
            }
        }

        List<VideoStreamInfo> videoStreams = [];
        List<AudioStreamInfo> audioStreams = [];
        List<SubtitleStreamInfo> subtitles = [];
        if (root.TryGetProperty("streams", out JsonElement streams))
        {
            foreach (JsonElement stream in streams.EnumerateArray())
            {
                string codecType = stream.TryGetProperty("codec_type", out JsonElement t)
                    ? (t.GetString() ?? "")
                    : "";

                switch (codecType)
                {
                    case "video":
                        videoStreams.Add(ParseVideo(stream));
                        break;
                    case "audio":
                        audioStreams.Add(ParseAudio(stream));
                        break;
                    case "subtitle":
                        subtitles.Add(ParseSubtitle(stream));
                        break;
                }
            }
        }

        List<ChapterInfo> chapters = [];
        if (root.TryGetProperty("chapters", out JsonElement chaptersElement))
        {
            foreach (JsonElement chapter in chaptersElement.EnumerateArray())
            {
                chapters.Add(ParseChapter(chapter));
            }
        }

        DiscTitle singleTitle = new(
            Index: 0,
            Name: discLabel,
            Duration: duration,
            VideoStreams: videoStreams.ToArray(),
            AudioStreams: audioStreams.ToArray(),
            Subtitles: subtitles.ToArray(),
            Chapters: chapters.ToArray(),
            EstimatedSizeBytes: 0,
            IsMainFeature: true
        );

        return new DiscInfo(discType, discLabel, [singleTitle], null, duration);
    }

    private static VideoStreamInfo ParseVideo(JsonElement stream) =>
        new(
            Index: stream.TryGetProperty("index", out JsonElement i) ? i.GetInt32() : 0,
            Codec: stream.TryGetProperty("codec_name", out JsonElement c)
                ? (c.GetString() ?? "")
                : "",
            Width: stream.TryGetProperty("width", out JsonElement w) ? w.GetInt32() : 0,
            Height: stream.TryGetProperty("height", out JsonElement h) ? h.GetInt32() : 0,
            FrameRate: 0,
            BitDepth: 8,
            PixelFormat: stream.TryGetProperty("pix_fmt", out JsonElement px)
                ? (px.GetString() ?? "")
                : "",
            ColorPrimaries: null,
            ColorTransfer: null,
            ColorSpace: null,
            IsDefault: false,
            BitRateKbps: 0
        );

    private static AudioStreamInfo ParseAudio(JsonElement stream)
    {
        string? language = null;
        if (
            stream.TryGetProperty("tags", out JsonElement tags)
            && tags.TryGetProperty("language", out JsonElement lang)
        )
        {
            language = lang.GetString();
        }

        return new AudioStreamInfo(
            Index: stream.TryGetProperty("index", out JsonElement i) ? i.GetInt32() : 0,
            Codec: stream.TryGetProperty("codec_name", out JsonElement c)
                ? (c.GetString() ?? "")
                : "",
            Channels: stream.TryGetProperty("channels", out JsonElement ch) ? ch.GetInt32() : 0,
            SampleRate: stream.TryGetProperty("sample_rate", out JsonElement sr)
            && int.TryParse(sr.GetString(), out int srInt)
                ? srInt
                : 0,
            BitRateKbps: 0,
            Language: language,
            IsDefault: false,
            IsForced: false
        );
    }

    private static SubtitleStreamInfo ParseSubtitle(JsonElement stream)
    {
        string? language = null;
        if (
            stream.TryGetProperty("tags", out JsonElement tags)
            && tags.TryGetProperty("language", out JsonElement lang)
        )
        {
            language = lang.GetString();
        }

        return new SubtitleStreamInfo(
            Index: stream.TryGetProperty("index", out JsonElement i) ? i.GetInt32() : 0,
            Codec: stream.TryGetProperty("codec_name", out JsonElement c)
                ? (c.GetString() ?? "")
                : "",
            Language: language,
            IsDefault: false,
            IsForced: false
        );
    }

    private static ChapterInfo ParseChapter(JsonElement chapter)
    {
        double start = 0;
        double end = 0;
        if (
            chapter.TryGetProperty("start_time", out JsonElement s)
            && double.TryParse(
                s.GetString(),
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture,
                out double startParsed
            )
        )
        {
            start = startParsed;
        }
        if (
            chapter.TryGetProperty("end_time", out JsonElement e)
            && double.TryParse(
                e.GetString(),
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture,
                out double endParsed
            )
        )
        {
            end = endParsed;
        }

        string? title = null;
        if (
            chapter.TryGetProperty("tags", out JsonElement tags)
            && tags.TryGetProperty("title", out JsonElement t)
        )
        {
            title = t.GetString();
        }

        return new ChapterInfo(TimeSpan.FromSeconds(start), TimeSpan.FromSeconds(end), title);
    }

    private static string TrimStderr(string stdErr)
    {
        if (string.IsNullOrEmpty(stdErr))
            return "(no stderr)";
        string[] lines = stdErr.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        return lines.Length <= 3 ? stdErr : string.Join('\n', lines[^3..]);
    }
}
