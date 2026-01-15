using Newtonsoft.Json;

namespace NoMercy.Api.Controllers.V1.Media.DTO.Components;

/// <summary>
/// Wrapper for NMHomeCard component matching Android app expectations.
/// </summary>
public record NMHomeCardWrapper
{
    [JsonProperty("id")] public string Id { get; set; } = string.Empty;
    [JsonProperty("title")] public string Title { get; set; } = string.Empty;
    [JsonProperty("data")] public HomeCardData? Data { get; set; }
    [JsonProperty("next_id")] public string? NextId { get; set; }
    [JsonProperty("previous_id")] public string? PreviousId { get; set; }
    [JsonProperty("more_link")] public string? MoreLink { get; set; }
    [JsonProperty("more_link_text")] public string? MoreLinkText { get; set; }
    [JsonProperty("watch")] public bool Watch { get; set; }
    [JsonProperty("context_menu_items")] public IEnumerable<ContextMenuItem> ContextMenuItems { get; set; } = [];
    [JsonProperty("url")] public string? Url { get; set; }
    [JsonProperty("properties")] public Dictionary<string, string>? Properties { get; set; }

    public NMHomeCardWrapper()
    {
    }

    public NMHomeCardWrapper(HomeCardData homeCardData)
    {
        Id = homeCardData.Id?.ToString() ?? string.Empty;
        Title = homeCardData.Title;
        Data = homeCardData;
    }
}
