using Newtonsoft.Json;

namespace NoMercy.Providers.Other;

public class RelationshipLinks
{
    [JsonProperty("self")] public Uri Self { get; set; } = default!;
    [JsonProperty("related")] public Uri Related { get; set; } = default!;
}