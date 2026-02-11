using Newtonsoft.Json;

namespace NoMercy.Api.DTOs.Media.Components;

/// <summary>
/// Base interface for all component props.
/// All components have an id and navigation properties.
/// </summary>
public interface IComponentProps
{
    [JsonProperty("id")] dynamic Id { get; set; }
    [JsonProperty("next_id")] dynamic? NextId { get; set; }
    [JsonProperty("previous_id")] dynamic? PreviousId { get; set; }
}

/// <summary>
/// Props for container components that can hold child components.
/// </summary>
public interface IContainerProps : IComponentProps
{
    [JsonProperty("title")] string Title { get; set; }
    [JsonProperty("more_link")] Uri? MoreLink { get; set; }
    [JsonProperty("more_link_text")] string? MoreLinkText { get; }
    [JsonProperty("items")] IEnumerable<ComponentEnvelope> Items { get; set; }
    [JsonProperty("contextMenuItems")] IEnumerable<ContextMenuItemDto>? ContextMenuItems { get; set; }
    [JsonProperty("url")] Uri? Url { get; set; }
    [JsonProperty("properties")] Dictionary<string, dynamic>? Properties { get; set; }
}

/// <summary>
/// Props for leaf components that hold data but cannot have children.
/// </summary>
/// <typeparam name="TData">The type of data this component displays.</typeparam>
public interface ILeafProps<TData> : IComponentProps
{
    [JsonProperty("title")] string Title { get; set; }
    [JsonProperty("data")] TData? Data { get; set; }
    [JsonProperty("watch")] bool Watch { get; set; }
    [JsonProperty("contextMenuItems")] IEnumerable<ContextMenuItemDto>? ContextMenuItems { get; set; }
    [JsonProperty("url")] Uri? Url { get; set; }
    [JsonProperty("properties")] Dictionary<string, dynamic>? Properties { get; set; }
}
