using Newtonsoft.Json;

namespace NoMercy.Api.DTOs.Media.Components;

/// <summary>
/// Wrapper for NMMusicCard component matching Android app expectations.
/// </summary>
public record NMMusicCardWrapper
{
    [JsonProperty("id")] public string Id { get; set; } = string.Empty;
    [JsonProperty("next_id")] public string? NextId { get; set; }
    [JsonProperty("previous_id")] public string? PreviousId { get; set; }
    [JsonProperty("more_link")] public string? MoreLink { get; set; }
    [JsonProperty("more_link_text")] public string? MoreLinkText { get; set; }
    [JsonProperty("watch")] public bool Watch { get; set; }
    [JsonProperty("data")] public MusicCardData Data { get; set; } = null!;
    [JsonProperty("contextMenuItems")] public IEnumerable<ContextMenuItem> ContextMenuItems { get; set; } = [];
    [JsonProperty("url")] public string? Url { get; set; }
    [JsonProperty("properties")] public Dictionary<string, string>? Properties { get; set; }

    public NMMusicCardWrapper()
    {
    }

    public NMMusicCardWrapper(MusicCardData musicCardData)
    {
        Id = musicCardData.Id ?? string.Empty;
        Data = musicCardData;
    }
}
