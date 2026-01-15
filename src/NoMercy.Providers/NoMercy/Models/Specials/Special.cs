using Newtonsoft.Json;

namespace NoMercy.Providers.NoMercy.Models.Specials;

public class Special
{
    [JsonProperty("id")] public Ulid Id { get; set; }
    [JsonProperty("backdrop")] public string? Backdrop { get; set; }
    [JsonProperty("description")] public string? Description { get; set; }
    [JsonProperty("poster")] public string? Poster { get; set; }
    [JsonProperty("logo")] public string? Logo { get; set; }
    [JsonProperty("title")] public string Title { get; set; } = string.Empty;
    [JsonProperty("titleSort")] public string? TitleSort { get; set; }
    [JsonProperty("creator")] public string? Creator { get; set; }
    [JsonProperty("overview")] public string? Overview { get; set; }
}