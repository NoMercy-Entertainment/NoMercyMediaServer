namespace NoMercy.Encoder.Subtitles;

using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using NoMercy.Encoder.Codecs;
using NoMercy.Encoder.Composition;
using NoMercy.Encoder.Infrastructure;

/// <summary>
/// Converts bitmap subtitle tracks (PGS / VobSub / DVB) into text subtitle files
/// by running FFmpeg's <c>ocr</c> filter with the libtesseract backend. Reads
/// the OCR output via the <c>metadata=print</c> filter, parses the
/// <c>lavfi.ocr.text</c> cue stream, and writes WebVTT or SRT.
/// </summary>
public partial class SubtitleOcrEngine(
    EncoderOptions options,
    IProcessRunner processRunner,
    ITesseractModelManager modelManager,
    ILogger<SubtitleOcrEngine> logger
) : ISubtitleOcrEngine
{
    public async Task<SubtitleTrack> OcrAsync(
        string inputPath,
        int streamIndex,
        string language,
        SubtitleCodecType outputFormat,
        CancellationToken ct
    )
    {
        // Pull the language model before invoking FFmpeg so the OCR filter
        // actually has training data when it runs.
        string modelPath = await modelManager.EnsureLanguageModelAsync(language, ct);
        string modelDirectory =
            Path.GetDirectoryName(modelPath)
            ?? throw new InvalidOperationException("Tesseract model has no parent directory");

        // OCR dumps its metadata to a file so we never have to parse FFmpeg's
        // interleaved stdout. Write it to a unique temp file per run so
        // concurrent OCR jobs can't collide.
        string tempDirectory = Path.Combine(Path.GetTempPath(), "nm-ocr");
        Directory.CreateDirectory(tempDirectory);
        string ocrOutput = Path.Combine(tempDirectory, $"ocr-{Guid.NewGuid():N}.txt");

        try
        {
            string[] args =
            [
                "-hide_banner",
                "-i",
                inputPath,
                "-f",
                "lavfi",
                "-i",
                "color=black:s=hd720",
                "-filter_complex",
                $"[0:s:{streamIndex}]ocr=language={language},metadata=print:key=lavfi.ocr.text:file={EscapeFilterPath(ocrOutput)}",
                "-an",
                "-f",
                "null",
                "-",
            ];

            ProcessResult result = await processRunner.RunAsync(
                options.FfmpegPath,
                args,
                onStdOut: null,
                onStdErr: null,
                workingDirectory: null,
                cancellationToken: ct
            );

            if (!result.IsSuccess)
            {
                throw new InvalidOperationException(
                    $"OCR ffmpeg exited with code {result.ExitCode}: {TrimErr(result.StdErr)}"
                );
            }

            if (!File.Exists(ocrOutput))
            {
                throw new InvalidOperationException(
                    "OCR produced no output file — check the subtitle stream index and language model."
                );
            }

            List<SubtitleCue> cues = ParseOcrOutput(await File.ReadAllTextAsync(ocrOutput, ct));
            string outputPath = Path.ChangeExtension(
                Path.Combine(Path.GetDirectoryName(inputPath)!, $"{language}_ocr"),
                outputFormat == SubtitleCodecType.Srt ? ".srt" : ".vtt"
            );

            if (outputFormat == SubtitleCodecType.Srt)
                await WriteSrtAsync(outputPath, cues, ct);
            else
                await WriteWebVttAsync(outputPath, cues, ct);

            logger.LogInformation(
                "OCR produced {CueCount} cues for {Language} → {Path}",
                cues.Count,
                language,
                outputPath
            );

            _ = modelDirectory; // referenced for clarity; filter resolves tessdata via TESSDATA_PREFIX if set

            return new SubtitleTrack(outputPath, language, outputFormat, cues.Count);
        }
        finally
        {
            if (File.Exists(ocrOutput))
            {
                try
                {
                    File.Delete(ocrOutput);
                }
                catch (IOException ex)
                {
                    logger.LogWarning(ex, "Could not delete OCR temp file {Path}", ocrOutput);
                }
            }
        }
    }

    /// <summary>
    /// Parses <c>metadata=print</c> output from FFmpeg's <c>ocr</c> filter.
    /// The filter emits one pts_time line per frame, optionally followed by
    /// <c>lavfi.ocr.text=</c> blocks. We stream the file line-by-line, tracking
    /// the current pts and the most recent text, and emit a cue every time the
    /// text changes — collapsing runs of identical text into one cue that spans
    /// the full duration the text was visible. Matches V1 behavior.
    /// </summary>
    internal static List<SubtitleCue> ParseOcrOutput(string content)
    {
        List<SubtitleCue> cues = [];

        string? currentText = null;
        double currentStart = 0;
        double currentLastSeen = 0;
        double latestPts = 0;

        foreach (string rawLine in content.Split('\n'))
        {
            string line = rawLine.TrimEnd('\r', ' ', '\t');
            if (string.IsNullOrEmpty(line))
                continue;

            Match ptsMatch = PtsTimeRegex().Match(line);
            if (ptsMatch.Success)
            {
                latestPts = double.Parse(
                    ptsMatch.Groups["pts"].Value,
                    CultureInfo.InvariantCulture
                );
                continue;
            }

            if (!line.StartsWith("lavfi.ocr.text=", StringComparison.Ordinal))
                continue;

            string text = line["lavfi.ocr.text=".Length..].Trim();

            if (string.IsNullOrEmpty(text))
            {
                if (currentText is not null)
                {
                    cues.Add(new SubtitleCue(currentStart, currentLastSeen, currentText));
                    currentText = null;
                }
                continue;
            }

            if (currentText is null)
            {
                currentText = text;
                currentStart = latestPts;
                currentLastSeen = latestPts;
            }
            else if (!string.Equals(currentText, text, StringComparison.Ordinal))
            {
                cues.Add(new SubtitleCue(currentStart, currentLastSeen, currentText));
                currentText = text;
                currentStart = latestPts;
                currentLastSeen = latestPts;
            }
            else
            {
                // Same text as current cue — extend its visible window to this pts.
                currentLastSeen = latestPts;
            }
        }

        if (currentText is not null)
            cues.Add(new SubtitleCue(currentStart, currentLastSeen, currentText));

        return cues;
    }

    private static async Task WriteWebVttAsync(
        string path,
        IEnumerable<SubtitleCue> cues,
        CancellationToken ct
    )
    {
        StringBuilder sb = new();
        sb.AppendLine("WEBVTT");
        sb.AppendLine();

        int index = 1;
        foreach (SubtitleCue cue in cues)
        {
            sb.AppendLine(index.ToString(CultureInfo.InvariantCulture));
            sb.AppendLine($"{FormatVttTime(cue.StartSeconds)} --> {FormatVttTime(cue.EndSeconds)}");
            sb.AppendLine(cue.Text);
            sb.AppendLine();
            index++;
        }

        await File.WriteAllTextAsync(path, sb.ToString(), ct);
    }

    private static async Task WriteSrtAsync(
        string path,
        IEnumerable<SubtitleCue> cues,
        CancellationToken ct
    )
    {
        StringBuilder sb = new();
        int index = 1;
        foreach (SubtitleCue cue in cues)
        {
            sb.AppendLine(index.ToString(CultureInfo.InvariantCulture));
            sb.AppendLine($"{FormatSrtTime(cue.StartSeconds)} --> {FormatSrtTime(cue.EndSeconds)}");
            sb.AppendLine(cue.Text);
            sb.AppendLine();
            index++;
        }

        await File.WriteAllTextAsync(path, sb.ToString(), ct);
    }

    private static string FormatVttTime(double seconds)
    {
        TimeSpan ts = TimeSpan.FromSeconds(seconds);
        return string.Format(
            CultureInfo.InvariantCulture,
            "{0:00}:{1:00}:{2:00}.{3:000}",
            (int)ts.TotalHours,
            ts.Minutes,
            ts.Seconds,
            ts.Milliseconds
        );
    }

    private static string FormatSrtTime(double seconds)
    {
        TimeSpan ts = TimeSpan.FromSeconds(seconds);
        return string.Format(
            CultureInfo.InvariantCulture,
            "{0:00}:{1:00}:{2:00},{3:000}",
            (int)ts.TotalHours,
            ts.Minutes,
            ts.Seconds,
            ts.Milliseconds
        );
    }

    private static string EscapeFilterPath(string path)
    {
        // FFmpeg filter args: backslashes and colons need escaping on Windows.
        return path.Replace('\\', '/').Replace(":", "\\:");
    }

    private static string TrimErr(string stdErr)
    {
        if (string.IsNullOrEmpty(stdErr))
            return "(no stderr)";
        string[] lines = stdErr.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        return lines.Length <= 5 ? stdErr : string.Join('\n', lines[^5..]);
    }

    [GeneratedRegex(@"pts_time:(?<pts>\d+(?:\.\d+)?)")]
    private static partial Regex PtsTimeRegex();

    public record SubtitleCue(double StartSeconds, double EndSeconds, string Text);
}
