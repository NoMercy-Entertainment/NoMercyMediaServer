using Newtonsoft.Json;

namespace NoMercy.Providers.Other;

public class Relationship
{
    [JsonProperty("links")] public RelationshipLinks Links { get; set; } = new();
}