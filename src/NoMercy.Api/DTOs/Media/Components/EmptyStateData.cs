using Newtonsoft.Json;

namespace NoMercy.Api.DTOs.Media.Components;

/// <summary>
/// Data payload for the NMEmptyState component.
/// Displayed on the home screen when there are no libraries or no scanned content.
/// </summary>
public record EmptyStateData
{
    [JsonProperty("title")] public string Title { get; set; } = string.Empty;
    [JsonProperty("message")] public string Message { get; set; } = string.Empty;
    [JsonProperty("icon")] public string Icon { get; set; } = string.Empty;
    [JsonProperty("action", NullValueHandling = NullValueHandling.Ignore)] public EmptyStateActionData? Action { get; set; }
    [JsonProperty("auto_refresh", NullValueHandling = NullValueHandling.Ignore)] public bool? AutoRefresh { get; set; }
}

/// <summary>
/// Optional call-to-action attached to an NMEmptyState component.
/// </summary>
public record EmptyStateActionData
{
    [JsonProperty("label")] public string Label { get; set; } = string.Empty;
    [JsonProperty("route")] public string Route { get; set; } = string.Empty;
}
