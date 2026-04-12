namespace NoMercy.Encoder.V3.PostProcess;

using System.Text;
using NoMercy.Encoder.V3.Analysis;

public class ChapterWriter
{
    public async Task WriteChaptersAsync(
        string outputDirectory,
        IReadOnlyList<ChapterInfo> chapters,
        CancellationToken ct
    )
    {
        if (chapters.Count == 0)
            return;

        StringBuilder sb = new();
        sb.AppendLine("WEBVTT");
        sb.AppendLine();

        for (int i = 0; i < chapters.Count; i++)
        {
            ChapterInfo chapter = chapters[i];
            sb.AppendLine($"Chapter {i + 1}");
            sb.AppendLine($"{FormatVttTime(chapter.Start)} --> {FormatVttTime(chapter.End)}");
            sb.AppendLine(chapter.Title ?? $"Chapter {i + 1}");
            sb.AppendLine();
        }

        string chaptersFile = Path.Combine(outputDirectory, "chapters.vtt");
        await File.WriteAllTextAsync(chaptersFile, sb.ToString(), ct);
    }

    private static string FormatVttTime(TimeSpan ts) =>
        $"{(int)ts.TotalHours:D2}:{ts.Minutes:D2}:{ts.Seconds:D2}.{ts.Milliseconds:D3}";
}
