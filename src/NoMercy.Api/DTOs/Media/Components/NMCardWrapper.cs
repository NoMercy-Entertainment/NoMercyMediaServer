using Newtonsoft.Json;

namespace NoMercy.Api.DTOs.Media.Components;

/// <summary>
/// Wrapper for NMCard and NMGenreCard components matching Android app expectations.
/// This is the props structure sent for both "NMCard" and "NMGenreCard" component types.
/// </summary>
public record NMCardWrapper
{
    [JsonProperty("id")] public string Id { get; set; } = string.Empty;
    [JsonProperty("title")] public string Title { get; set; } = string.Empty;
    [JsonProperty("data")] public CardData? Data { get; set; }
    [JsonProperty("next_id")] public string? NextId { get; set; }
    [JsonProperty("previous_id")] public string? PreviousId { get; set; }
    [JsonProperty("more_link")] public string? MoreLink { get; set; }
    [JsonProperty("more_link_text")] public string? MoreLinkText { get; set; }
    [JsonProperty("watch")] public bool Watch { get; set; }
    [JsonProperty("contextMenuItems")] public IEnumerable<ContextMenuItem> ContextMenuItems { get; set; } = [];
    [JsonProperty("url")] public string? Url { get; set; }
    [JsonProperty("properties")] public Dictionary<string, string>? Properties { get; set; }

    public NMCardWrapper()
    {
    }

    public NMCardWrapper(CardData cardData)
    {
        Id = cardData.Id?.ToString() ?? string.Empty;
        Title = cardData.Title;
        Data = cardData;
    }
}
