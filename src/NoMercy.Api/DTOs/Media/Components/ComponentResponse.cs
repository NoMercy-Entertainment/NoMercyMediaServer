using Newtonsoft.Json;

namespace NoMercy.Api.DTOs.Media.Components;

/// <summary>
/// Standard API response wrapper for component-based responses.
/// </summary>
public record ComponentResponse
{
    [JsonProperty("id")] public Ulid Id { get; set; } = Ulid.NewUlid();
    [JsonProperty("data")] public IEnumerable<ComponentEnvelope> Data { get; set; } = [];

    public ComponentResponse()
    {
    }

    public ComponentResponse(params ComponentEnvelope[] components)
    {
        Data = components;
    }

    public ComponentResponse(IEnumerable<ComponentEnvelope> components)
    {
        Data = components;
    }

    /// <summary>
    /// Creates a response with a single component.
    /// </summary>
    public static ComponentResponse From(ComponentEnvelope component) => new([component]);

    /// <summary>
    /// Creates a response with multiple components.
    /// </summary>
    public static ComponentResponse From(params ComponentEnvelope[] components) => new(components);

    /// <summary>
    /// Creates a response from a collection of components.
    /// </summary>
    public static ComponentResponse From(IEnumerable<ComponentEnvelope> components) => new(components);
}

/// <summary>
/// Response wrapper that includes both render data and source metadata for internal use.
/// </summary>
public record ComponentRenderResponse : ComponentResponse
{
    /// <summary>
    /// Internal source metadata (not serialized to client).
    /// </summary>
    [JsonIgnore]
    public IEnumerable<ComponentSourceMetadata> Sources { get; set; } = [];
}

/// <summary>
/// Internal metadata for tracking component data sources (not sent to client).
/// </summary>
public record ComponentSourceMetadata
{
    public int Id { get; set; }
    public string MediaType { get; set; } = string.Empty;
}
