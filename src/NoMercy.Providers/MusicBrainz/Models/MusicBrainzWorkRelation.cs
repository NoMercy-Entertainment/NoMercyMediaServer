using Newtonsoft.Json;

namespace NoMercy.Providers.MusicBrainz.Models;
public class MusicBrainzWorkRelation : MusicBrainzLifeSpan
{
    [JsonProperty("attribute-ids")] public Dictionary<string, Guid> AttributeIds { get; set; } = new();
    [JsonProperty("attribute-values")] public MusicBrainzAttributeValues MusicBrainzAttributeValues { get; set; } = new();
    [JsonProperty("attributes")] public string[] Attributes { get; set; } = [];
    [JsonProperty("direction")] public string Direction { get; set; } = string.Empty;
    [JsonProperty("source-credit")] public string SourceCredit { get; set; } = string.Empty;
    [JsonProperty("target-credit")] public string TargetCredit { get; set; } = string.Empty;
    [JsonProperty("target-type")] public string TargetType { get; set; } = string.Empty;
    [JsonProperty("type")] public string Type { get; set; } = string.Empty;
    [JsonProperty("type-id")] public Guid? TypeId { get; set; }
    [JsonProperty("artist")] public MusicBrainzArtist MusicBrainzArtist { get; set; } = new();
    [JsonProperty("label")] public MusicBrainzLabel MusicBrainzLabel { get; set; }  = new();
    [JsonProperty("url")] public MusicBrainzUrl MusicBrainzUrl { get; set; } = new();
}