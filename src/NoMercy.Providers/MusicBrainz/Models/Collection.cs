using Newtonsoft.Json;

namespace NoMercy.Providers.MusicBrainz.Models;
public class Collection
{
    [JsonProperty("editor")] public string Editor { get; set; } = string.Empty;
    [JsonProperty("entity-type")] public string EntityType { get; set; } = string.Empty;
    [JsonProperty("id")] public Guid Id { get; set; }
    [JsonProperty("name")] public string Name { get; set; } = string.Empty;
    [JsonProperty("release-count")] public int ReleaseCount { get; set; }
    [JsonProperty("type")] public string Type { get; set; } = string.Empty;
    [JsonProperty("type-id")] public Guid? TypeId { get; set; }
}