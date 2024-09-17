using Newtonsoft.Json;

namespace NoMercy.Providers.MusicBrainz.Models;
public class MusicBrainzWork
{
    [JsonProperty("attributes")] public object[] Attributes { get; set; }
    [JsonProperty("disambiguation")] public string Disambiguation { get; set; }
    [JsonProperty("id")] public Guid Id { get; set; }
    [JsonProperty("iswcs")] public string[] Iswcs { get; set; }
    [JsonProperty("language")] public string Language { get; set; }
    [JsonProperty("languages")] public string[] Languages { get; set; }
    [JsonProperty("relations")] public MusicBrainzWorkRelation[] Relations { get; set; }
    [JsonProperty("title")] public string Title { get; set; }
    [JsonProperty("type")] public string Type { get; set; }
    [JsonProperty("type-id")] public Guid? TypeId { get; set; }
}