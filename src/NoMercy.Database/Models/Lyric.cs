using Newtonsoft.Json;

namespace NoMercy.Database.Models;

public class Lyric
{
    [JsonProperty("text")] public string Text { get; set; } = string.Empty;
    [JsonProperty("time")] public LineTime Time { get; set; } = new();

    public class LineTime
    {
        [JsonProperty("total")] public double Total;
        [JsonProperty("minutes")] public int Minutes;
        [JsonProperty("seconds")] public int Seconds;
        [JsonProperty("hundredths")] public int Hundredths;
    }
}