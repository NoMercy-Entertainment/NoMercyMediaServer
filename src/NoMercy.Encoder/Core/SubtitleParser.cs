using System.Globalization;
using System.Text.RegularExpressions;

namespace NoMercy.Encoder.Core;

public partial class SubtitleParser
{
    public static Subtitle[] ParseSubtitles(string filePath)
    {
        List<Subtitle> subtitles = [];
        string fileContent = File.ReadAllText(filePath);

        MatchCollection matches = OcrRegex().Matches(fileContent);

        foreach (Match match in matches)
        {
            double startTime = double.Parse(
                match.Groups["start"].Value,
                CultureInfo.InvariantCulture
            );
            double endTime = double.Parse(match.Groups["end"].Value, CultureInfo.InvariantCulture);
            string text = PostProcessOcrText(match.Groups["text"].Value.Trim());

            subtitles.Add(new(startTime, endTime, text));
        }

        return subtitles.ToArray();
    }

    /// <summary>
    /// Fixes common OCR misrecognitions from Tesseract on bitmap subtitles.
    /// DVD/Blu-ray subtitle bitmaps contain glyphs that Tesseract cannot recognize
    /// (e.g. ♪) and consistently misreads as ASCII characters.
    /// </summary>
    private static string PostProcessOcrText(string text)
    {
        // Fix music note symbols: Tesseract reads ♪ as &, J, I, or ' at the start of lines.
        // Pattern: line starts with a single non-word character followed by a space and an uppercase letter.
        // Also handles dialog prefix "- " before the misread music note.
        text = MusicNoteRegex().Replace(text, "${prefix}♪ ");

        // Fix double music notes (e.g. "J'" or "&'" at start of line)
        text = DoubleMusicNoteRegex().Replace(text, "${prefix}♪ ");

        return text;
    }

    [GeneratedRegex(@"^(?<prefix>- ?)?[&JI'] (?=[A-Z])", RegexOptions.Multiline)]
    private static partial Regex MusicNoteRegex();

    [GeneratedRegex(@"^(?<prefix>- ?)?[&JI][&JI'] (?=[A-Z])", RegexOptions.Multiline)]
    private static partial Regex DoubleMusicNoteRegex();

    public static void SaveToVtt(Subtitle[] subtitles, string filePath)
    {
        using StreamWriter writer = new(filePath);
        writer.WriteLine("WEBVTT");
        writer.WriteLine();

        foreach (Subtitle subtitle in subtitles)
        {
            writer.WriteLine(
                $"{FormatTime(subtitle.StartTime)} --> {FormatTime(subtitle.EndTime)}"
            );
            writer.WriteLine(subtitle.Text);
            writer.WriteLine();
        }
    }

    private static string FormatTime(double timeInSeconds)
    {
        TimeSpan timeSpan = TimeSpan.FromSeconds(timeInSeconds);
        return string.Format(
            "{0:00}:{1:00}:{2:00}.{3:000}",
            timeSpan.Hours,
            timeSpan.Minutes,
            timeSpan.Seconds,
            timeSpan.Milliseconds
        );
    }

    [GeneratedRegex(
        @"pts_time:(?<start>\d+(\.\d+)?)\nlavfi\.ocr\.text=(?<text>.+(\n.+)?)\n\n.*?pts_time:(?<end>\d+(\.\d+)?)",
        RegexOptions.Multiline
    )]
    private static partial Regex OcrRegex();
}

public class Subtitle
{
    public double StartTime { get; }
    public double EndTime { get; }
    public string Text { get; }

    public Subtitle(double startTime, double endTime, string text)
    {
        StartTime = startTime;
        EndTime = endTime;
        Text = text;
    }
}
