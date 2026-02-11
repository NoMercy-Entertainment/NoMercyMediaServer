using Newtonsoft.Json;

namespace NoMercy.Api.DTOs.Media.Components;

/// <summary>
/// The envelope that wraps all components. This is the main type sent over the wire.
/// Uses discriminated union pattern where 'component' field determines the props type.
/// </summary>
public record ComponentEnvelope
{
    [JsonProperty("id")] public Ulid Id { get; set; } = Ulid.NewUlid();
    [JsonProperty("component")] public string Component { get; set; } = string.Empty;
    [JsonProperty("props")] public object Props { get; set; } = new();
    [JsonProperty("update", NullValueHandling = NullValueHandling.Ignore)] public UpdateDto? Update { get; set; }
    [JsonProperty("replacing", NullValueHandling = NullValueHandling.Ignore)] public Ulid? Replacing { get; set; }
}

/// <summary>
/// Extension methods for creating ComponentEnvelopes fluently.
/// </summary>
public static class ComponentEnvelopeExtensions
{
    public static ComponentEnvelope WithId(this ComponentEnvelope envelope, Ulid id)
    {
        envelope.Id = id;
        return envelope;
    }

    public static ComponentEnvelope WithUpdate(this ComponentEnvelope envelope, UpdateDto? update)
    {
        envelope.Update = update;
        return envelope;
    }

    public static ComponentEnvelope WithReplacing(this ComponentEnvelope envelope, Ulid replacingId)
    {
        envelope.Replacing = replacingId;
        return envelope;
    }
}
