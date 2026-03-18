using Newtonsoft.Json;

namespace NoMercy.Providers.Other;

public class Data
{
    [JsonProperty("id")] public int Id { get; set; }
    [JsonProperty("type")] public string Type { get; set; } = string.Empty;
    [JsonProperty("links")] public Links Links { get; set; } = new();
    [JsonProperty("attributes")] public Attributes Attributes { get; set; } = new();
    [JsonProperty("relationships")] public Dictionary<string, Relationship> Relationships { get; set; } = new();
}