using Newtonsoft.Json;
using NoMercy.Api.DTOs.Music;
using NoMercy.NmSystem.Extensions;

namespace NoMercy.Api.DTOs.Media;

public record Render
{
    [JsonProperty("id")] public Ulid Id { get; set; } = Ulid.NewUlid();
    [JsonProperty("data")] public IEnumerable<object> Data { get; set; } = [];
}

public record ComponentDto<T>
{
    public ComponentDto()
    {
        Id = Ulid.NewUlid();
        Update = new(Id)
        {
            When = null
        };
    }

    [JsonProperty("id")] public Ulid Id { get; set; }
    [JsonProperty("component")] public string Component { get; set; } = string.Empty;
    [JsonProperty("props")] public RenderProps<T> Props { get; set; } = new();
    [JsonProperty("update")] public Update Update { get; set; }
    [JsonProperty("replacing")] public Ulid Replacing { get; set; }
}

public record Update
{
    [JsonProperty("when")] public string? When { get; set; }
    [JsonProperty("link")] public Uri Link { get; set; } = default!;
    [JsonProperty("body")] public object Body { get; set; } = new();

    public Update(Ulid ulid)
    {
        Body = new
        {
            replace_id = ulid
        };
    }
}

public record RenderProps<T>
{
    [JsonProperty("id")] public dynamic Id { get; set; } = Ulid.NewUlid();
    [JsonProperty("next_id")] public dynamic NextId { get; set; } = Ulid.NewUlid();
    [JsonProperty("previous_id")] public dynamic PreviousId { get; set; } = Ulid.NewUlid();
    [JsonProperty("title")] public string Title { get; set; } = string.Empty;
    [JsonProperty("more_link")] public Uri? MoreLink { get; set; }
    [JsonProperty("more_link_text")] public string? MoreText => MoreLink is not null ? "See all".Localize() : null;
    [JsonProperty("items")] public IEnumerable<ComponentDto<T>>? Items { get; set; } = [];
    [JsonProperty("data")] public T? Data { get; set; }
    [JsonProperty("watch")] public bool Watch { get; set; }
    [JsonProperty("contextMenuItems")] public Dictionary<string, object>[]? ContextMenuItems { get; set; } = [];
    [JsonProperty("url")] public Uri? Url { get; set; }
    [JsonProperty("displayList")] public IEnumerable<ArtistTrackDto>? DisplayList { get; set; } = [];
    [JsonProperty("properties")] public Dictionary<string, dynamic> Properties { get; set; } = new();
}