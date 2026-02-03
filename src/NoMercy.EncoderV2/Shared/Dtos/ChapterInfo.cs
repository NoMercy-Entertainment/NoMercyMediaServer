using Newtonsoft.Json;

namespace NoMercy.EncoderV2.Shared.Dtos;

public class FfprobeChapterRoot
{
    [JsonProperty("chapters")] public FfprobeChapter[] Chapters { get; set; } = [];
}

public class FfprobeChapter
{
    [JsonProperty("id")] public double Id { get; set; }
    [JsonProperty("time_base")] public string TimeBase { get; set; } = string.Empty;
    [JsonProperty("start")] public long Start { get; set; }
    [JsonProperty("start_time")] public double StartTime { get; set; }
    [JsonProperty("end")] public long End { get; set; }
    [JsonProperty("end_time")] public double EndTime { get; set; }
    [JsonProperty("tags")] public FfprobeTags FfprobeTags { get; set; } = new();
}

public class FfprobeTags
{
    [JsonProperty("title")] public string Title { get; set; } = string.Empty;
}