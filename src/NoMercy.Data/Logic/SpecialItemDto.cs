using Newtonsoft.Json;

namespace NoMercy.Data.Logic;

public class SpecialItemDto
{
    [JsonProperty("id")] public int Id { get; set; }
    [JsonProperty("type")] public string Type { get; set; } = string.Empty;
}