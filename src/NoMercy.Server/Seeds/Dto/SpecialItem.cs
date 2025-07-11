using Newtonsoft.Json;

namespace NoMercy.Server.Seeds.Dto;

public class SpecialItem
{
    [JsonProperty("index")] public int Index { get; set; }
    [JsonProperty("type")] public string Type { get; set; } = string.Empty;
    [JsonProperty("title")] public string Title { get; set; } = string.Empty;
    [JsonProperty("year")] public int Year { get; set; }
    [JsonProperty("seasons")] public int[] Seasons { get; set; } = [];
    [JsonProperty("episodes")] public int[] Episodes { get; set; } = [];
}