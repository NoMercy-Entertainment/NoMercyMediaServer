using System.Text;
using Newtonsoft.Json;
using NoMercy.NmSystem.Extensions;

namespace NoMercy.Encoder.Core;

public static class Chapters
{
    public static async Task Extract(string inputFilePath, string location)
    {
        string chapterFile = $"{location}/chapters.vtt";

        string command = $"-v quiet -print_format json -show_chapters \"{inputFilePath}\"";
        string result = await FfMpeg.Ffprobe(command);
        if (string.IsNullOrEmpty(result)) return;

        FfprobeChapterRoot? root = JsonConvert.DeserializeObject<FfprobeChapterRoot>(result);
        if (root?.Chapters is null) return;
        if (root.Chapters.Length == 0) return;

        StringBuilder sb = new();

        sb.AppendLine("WEBVTT");
        sb.AppendLine();

        foreach (FfprobeChapter chapter in root.Chapters)
        {
            int id = Array.IndexOf(root.Chapters, chapter) + 1;
            sb.AppendLine($"Chapter {id}");
            sb.AppendLine($"{chapter.StartTime.ToHis()} --> {chapter.EndTime.ToHis()}");
            sb.AppendLine($"{chapter.FfprobeTags.Title}");
            sb.AppendLine();
        }

        await File.WriteAllTextAsync(chapterFile, sb.ToString());
    }

    private class FfprobeChapterRoot
    {
        [JsonProperty("chapters")] public FfprobeChapter[] Chapters { get; set; } = [];
    }

    private class FfprobeChapter
    {
        [JsonProperty("id")] public double Id { get; set; }
        [JsonProperty("time_base")] public string TimeBase { get; set; } = string.Empty;
        [JsonProperty("start")] public long Start { get; set; }
        [JsonProperty("start_time")] public double StartTime { get; set; }
        [JsonProperty("end")] public long End { get; set; }
        [JsonProperty("end_time")] public double EndTime { get; set; }
        [JsonProperty("tags")] public FfprobeTags FfprobeTags { get; set; } = new();
    }

    private class FfprobeTags
    {
        [JsonProperty("title")] public string Title { get; set; } = string.Empty;
    }
}