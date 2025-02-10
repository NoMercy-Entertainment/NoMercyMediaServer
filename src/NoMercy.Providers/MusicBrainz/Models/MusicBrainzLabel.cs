using Newtonsoft.Json;

namespace NoMercy.Providers.MusicBrainz.Models;
public class MusicBrainzLabel
{
    [JsonProperty("aliases")] public Alias[] Aliases { get; set; } = [];
    [JsonProperty("disambiguation")] public string Disambiguation { get; set; } = string.Empty;
    [JsonProperty("genres")] public MusicBrainzGenreDetails[] Genres { get; set; } = [];
    [JsonProperty("id")] public Guid Id { get; set; }
    [JsonProperty("label-code")] public string? LabelCode { get; set; }
    [JsonProperty("name")] public string Name { get; set; } = string.Empty;
    [JsonProperty("sort-name")] public string SortName { get; set; } = string.Empty;
    [JsonProperty("tags")] public MusicBrainzTag[] Tags { get; set; } = [];
    [JsonProperty("type")] public string Type { get; set; } = string.Empty;
    [JsonProperty("type-id")] public Guid? TypeId { get; set; }
}