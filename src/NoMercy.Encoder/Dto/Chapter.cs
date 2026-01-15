using Newtonsoft.Json;
using NoMercy.NmSystem.Extensions;

namespace NoMercy.Encoder.Dto;

public class Chapter
{
    [JsonProperty("id")] public double Id { get; set; }
    [JsonProperty("title")] public string? Title { get; set; }
    
    [JsonProperty("start")] public long Start { get; set; }
    [JsonProperty("end")] public long End { get; set; }
    
    [JsonProperty("start_time")] public double StartTime { get; set; }
    [JsonProperty("end_time")] public double EndTime { get; set; }
    
    [JsonProperty("start_his")] public string StartHis => StartTime.ToHis();
    [JsonProperty("end_his")] public string EndHis => EndTime.ToHis();
    
    [JsonProperty("time_base")] public string? TimeBase { get; set; }
    
    public Chapter(FfprobeSourceDataChapter ffprobeSourceDataChapter, int index)
    {
        Id = index + 1;
        Title = ffprobeSourceDataChapter.Tags?.Title;
        Start = ffprobeSourceDataChapter.Start;
        End = ffprobeSourceDataChapter.End;
        StartTime = ffprobeSourceDataChapter.StartTime;
        EndTime = ffprobeSourceDataChapter.EndTime;
        TimeBase = ffprobeSourceDataChapter.TimeBase;
    }

}