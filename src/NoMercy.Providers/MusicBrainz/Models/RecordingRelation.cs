using Newtonsoft.Json;

namespace NoMercy.Providers.MusicBrainz.Models;
public class RecordingRelation : MusicBrainzLifeSpan
{
    [JsonProperty("attribute-ids")] public Dictionary<string, Guid> AttributeIds { get; set; } = new();
    [JsonProperty("attribute-values")] public MusicBrainzAttributeValues MusicBrainzAttributeValues { get; set; } = new();
    [JsonProperty("attributes")] public string[] Attributes { get; set; } = [];
    [JsonProperty("source-credit")] public string SourceCredit { get; set; } = string.Empty;
    [JsonProperty("target-credit")] public string TargetCredit { get; set; } = string.Empty;
    [JsonProperty("target-type")] public string TargetType { get; set; } = string.Empty;
    [JsonProperty("type")] public string Type { get; set; } = string.Empty;
    [JsonProperty("direction")] public string Direction { get; set; } = string.Empty;
    [JsonProperty("type-id")] public Guid? TypeId { get; set; }
    [JsonProperty("artist")] public PurpleArtist Artist { get; set; } = new();
    [JsonProperty("attribute-credits")] public AttributeCredits AttributeCredits { get; set; } = new();
    [JsonProperty("label")] public PurpleArtist Label { get; set; } = new();
    [JsonProperty("work")] public MusicBrainzWork MusicBrainzWork { get; set; } = new();
    [JsonProperty("recording")] public RelationRecording Recording { get; set; } = new();
}