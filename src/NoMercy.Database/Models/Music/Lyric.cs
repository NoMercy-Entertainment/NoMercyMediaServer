using Newtonsoft.Json;
using NoMercy.NmSystem.Extensions;

namespace NoMercy.Database.Models.Music;

public class Lyric
{
    [JsonProperty("text")] public string Text { get; set; } = string.Empty;
    [JsonProperty("time")] public LineTime Time { get; set; } = new();
    [JsonProperty("rtl")] public bool Rtl => Text.GetTextDirection() == Str.TextDirection.RTL;

    public class LineTime
    {
        [JsonProperty("total")] public double Total;
        [JsonProperty("minutes")] public int Minutes;
        [JsonProperty("seconds")] public int Seconds;
        [JsonProperty("hundredths")] public int Hundredths;
    }
}