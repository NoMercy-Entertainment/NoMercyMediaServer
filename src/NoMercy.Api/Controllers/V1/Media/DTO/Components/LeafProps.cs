using Newtonsoft.Json;

namespace NoMercy.Api.Controllers.V1.Media.DTO.Components;

/// <summary>
/// Base implementation for leaf component props.
/// Leaf components hold data but cannot have children.
/// </summary>
/// <typeparam name="TData">The type of data this component displays.</typeparam>
public record LeafProps<TData> : ILeafProps<TData>
{
    [JsonProperty("id")] public dynamic Id { get; set; } = Ulid.NewUlid();
    [JsonProperty("next_id", NullValueHandling = NullValueHandling.Ignore)] public dynamic? NextId { get; set; }
    [JsonProperty("previous_id", NullValueHandling = NullValueHandling.Ignore)] public dynamic? PreviousId { get; set; }
    [JsonProperty("title")] public string Title { get; set; } = string.Empty;
    [JsonProperty("data")] public TData? Data { get; set; }
    [JsonProperty("watch")] public bool Watch { get; set; }
    [JsonProperty("contextMenuItems", NullValueHandling = NullValueHandling.Ignore)] public IEnumerable<ContextMenuItemDto>? ContextMenuItems { get; set; }
    [JsonProperty("url", NullValueHandling = NullValueHandling.Ignore)] public Uri? Url { get; set; }
    [JsonProperty("properties", NullValueHandling = NullValueHandling.Ignore)] public Dictionary<string, dynamic>? Properties { get; set; }
}

/// <summary>
/// Props for NMCard component - standard media card.
/// </summary>
public record CardProps : LeafProps<CardData>;

/// <summary>
/// Props for NMHomeCard component - featured home page card with video support.
/// </summary>
public record HomeCardProps : LeafProps<HomeCardData>;

/// <summary>
/// Props for NMGenreCard component - genre category card.
/// </summary>
public record GenreCardProps : LeafProps<GenreCardData>;

/// <summary>
/// Props for NMMusicCard component - music album/artist card.
/// </summary>
public record MusicCardProps : LeafProps<MusicCardData>;

/// <summary>
/// Props for NMMusicHomeCard component - music home featured card.
/// </summary>
public record MusicHomeCardProps : LeafProps<MusicHomeCardData>;

/// <summary>
/// Props for NMTrackRow component - single track in a list.
/// </summary>
public record TrackRowProps : LeafProps<TrackRowData>
{
    [JsonProperty("displayList", NullValueHandling = NullValueHandling.Ignore)] public IEnumerable<TrackRowData>? DisplayList { get; set; }
}

/// <summary>
/// Props for NMTopResultCard component - search top result.
/// </summary>
public record TopResultCardProps : LeafProps<TopResultCardData>;

/// <summary>
/// Props for NMSeasonCard component - episode in a season.
/// </summary>
public record SeasonCardProps : LeafProps<SeasonCardData>;

/// <summary>
/// Props for NMSeasonTitle component - season header.
/// </summary>
public record SeasonTitleProps : LeafProps<SeasonTitleData>;
