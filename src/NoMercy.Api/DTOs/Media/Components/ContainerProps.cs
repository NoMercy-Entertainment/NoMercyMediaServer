using Newtonsoft.Json;
using NoMercy.NmSystem;
using NoMercy.NmSystem.Extensions;

namespace NoMercy.Api.DTOs.Media.Components;

/// <summary>
/// Base implementation for container component props.
/// Container components can hold child components.
/// </summary>
public record ContainerProps : IContainerProps
{
    [JsonProperty("id")] public dynamic Id { get; set; } = Ulid.NewUlid();
    [JsonProperty("next_id", NullValueHandling = NullValueHandling.Ignore)] public dynamic? NextId { get; set; }
    [JsonProperty("previous_id", NullValueHandling = NullValueHandling.Ignore)] public dynamic? PreviousId { get; set; }
    [JsonProperty("title")] public string Title { get; set; } = string.Empty;
    [JsonProperty("more_link", NullValueHandling = NullValueHandling.Ignore)] public Uri? MoreLink { get; set; }
    [JsonProperty("more_link_text", NullValueHandling = NullValueHandling.Ignore)] public string? MoreLinkText => MoreLink is not null ? "See all".Localize() : null;
    [JsonProperty("items")] public IEnumerable<ComponentEnvelope> Items { get; set; } = [];
    [JsonProperty("contextMenuItems", NullValueHandling = NullValueHandling.Ignore)] public IEnumerable<ContextMenuItemDto>? ContextMenuItems { get; set; }
    [JsonProperty("url", NullValueHandling = NullValueHandling.Ignore)] public Uri? Url { get; set; }
    [JsonProperty("properties", NullValueHandling = NullValueHandling.Ignore)] public Dictionary<string, dynamic>? Properties { get; set; }
}

/// <summary>
/// Props for NMGrid component - displays items in a grid layout.
/// </summary>
public record GridProps : ContainerProps
{
    [JsonProperty("columns", NullValueHandling = NullValueHandling.Ignore)] public int? Columns { get; set; }
    [JsonProperty("gap", NullValueHandling = NullValueHandling.Ignore)] public int? Gap { get; set; }
}

/// <summary>
/// Props for NMList component - displays items in a vertical list.
/// </summary>
public record ListProps : ContainerProps
{
    [JsonProperty("orientation", NullValueHandling = NullValueHandling.Ignore)] public string? Orientation { get; set; }
}

/// <summary>
/// Props for NMCarousel component - displays items in a horizontal scrollable carousel.
/// </summary>
public record CarouselProps : ContainerProps
{
    [JsonProperty("auto_scroll", NullValueHandling = NullValueHandling.Ignore)] public bool? AutoScroll { get; set; }
    [JsonProperty("scroll_interval", NullValueHandling = NullValueHandling.Ignore)] public int? ScrollInterval { get; set; }
}

/// <summary>
/// Props for NMContainer component - generic container for grouping components.
/// </summary>
public record NmContainerProps : ContainerProps
{
    [JsonProperty("layout", NullValueHandling = NullValueHandling.Ignore)] public string? Layout { get; set; }
}
