using Newtonsoft.Json;
using NoMercy.Providers.TMDB.Models.Shared;

namespace NoMercy.Providers.TMDB.Models.People;

public class TmdbTaggedImage : TmdbProfile
{
    [JsonProperty("id")] public string Id { get; set; } = string.Empty;
    [JsonProperty("image_type")] public string ImageType { get; set; } = string.Empty;
    [JsonProperty("media")] public TmdbPersonMedia TmdbPersonMedia { get; set; } = new();
    [JsonProperty("media_type")] public string MediaType { get; set; } = string.Empty;
}